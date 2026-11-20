using System;
using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;
using OpenNetMeter.Properties;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace OpenNetMeter.Models
{
    [SupportedOSPlatform("windows")]
    public partial class NetworkProcess : IDisposable
    {
        //---------- Constants ------------//

        private const int OneSec = 1000;
        private const int DebounceDelayMs = 300; // Delay before processing network change events

        // Default IP values indicating "not connected"
        private static readonly byte[] DefaultIPv4 = { 0, 0, 0, 0 };
        private static readonly byte[] DefaultIPv6 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        //---------- Network State ------------//

        /// <summary>
        /// Immutable snapshot of current network connection state.
        /// Used to detect connection changes without race conditions.
        /// </summary>
        private sealed record NetworkSnapshot(
            string AdapterName,  // Display name, includes SSID for Wi-Fi
            string AdapterId,    // GUID for the adapter
            byte[] IPv4,         // Assigned IPv4 address bytes
            byte[] IPv6          // Assigned IPv6 address bytes
        );

        // Lock for thread-safe access to local IP addresses
        private readonly object stateLock = new object();

        // Currently assigned local IP addresses (used for packet filtering)
        private byte[] localIPv4 = DefaultIPv4;
        private byte[] localIPv6 = DefaultIPv6;

        // Serialises the one-second speed tick (which raises the traffic batch) against an adapter switch, so a
        // tick can never stage the new adapter's traffic into the old adapter's database buffer, or the other way
        // round. Held while events are raised; subscribers only post to their own threads.
        private readonly object tickLock = new object();

        // The speed tick's baseline: the session totals at the previous tick. Guarded by tickLock.
        private long speedBaselineDownload;
        private long speedBaselineUpload;

        //---------- Debounce & Synchronization ------------//

        // Timer-based debounce: each network change event resets the timer.
        // When it finally fires (after DebounceDelayMs of silence), HandleNetworkChange runs.
        private Timer? debounceTimer;
        private volatile bool isDisposed;

        // Routing table change notifications (automatic adapter selection depends on the routes)
        private IDisposable? routeChangeSubscription;

        // Lock to ensure only one HandleNetworkChange runs at a time
        private readonly object networkChangeLock = new object();

        //---------- Periodic Tasks ------------//

        // Periodic task for updating network speed display
        private PeriodicWork? networkSpeedWork;

        // Periodic task for pushing process data to database
        private PeriodicWork? dbPushWork;

        //---------- ETW Session ------------//

        // ETW kernel session for capturing network packets. It lives from StartNetworkProcess to EndNetworkProcess
        // and is deliberately kept across adapter switches; it only filters by local IP, which a switch changes.
        private IKernelCaptureSession? kernelSession;

        private const string KernelSessionName = "OpenNetMeter-Kernel";

        // Task running the ETW packet capture loop
        public Task? PacketTask;

        // Guards kernelSession, PacketTask and the health-check state below. Not held while waiting for the task.
        private readonly object captureLock = new object();

        // True from the start of capture until StopKernelSession asks it to stop: a capture task that ends while
        // this is set ended on its own.
        private bool captureWanted;

        // Health check (see CheckCaptureHealth). Guarded by captureLock.
        private const int CaptureRestartCooldownTicks = 5;      // seconds between restart attempts
        private const int CaptureHealthyTicksToRearmLog = 60;   // healthy seconds before a new failure is logged again
        private int captureRestartCooldown;
        private int captureHealthyTicks;
        private bool captureFailureLogged;

        // Creates the capture session and starts tracing, handing it the packet handler. Tests replace it so no
        // real ETW session is started.
        private Func<PacketHandler, IKernelCaptureSession> kernelCaptureFactory =
            handler => new EtwKernelCaptureSession(KernelSessionName, handler);

        // Where capture, session and database-setup errors are reported (a null exception is a plain error message).
        // Tests replace it so they don't write to the Windows Event Log.
        internal Action<string, Exception?> ErrorLogger { get; set; } = (message, ex) =>
        {
            if (ex == null)
                EventLogger.Error(message);
            else
                EventLogger.Error(message, ex);
        };

        /// <summary>
        /// Asks the owner to hand over, synchronously, everything counted since the last tick (to the UI and to
        /// <see cref="PushToDBBuffer"/>). Raised while the adapter switches so the old adapter's last partial
        /// second is not carried over to the new one.
        /// </summary>
        internal event Action? TrafficFlushRequested;

        //---------- Public State ------------//

        // Current network adapter name (includes SSID for Wi-Fi connections)
        public string AdapterName { get; private set; } = "";

        // Current adapter's unique ID (GUID) - used for comparison to detect changes
        private string currentAdapterId = "";
        public string CurrentAdapterId => currentAdapterId;

        /// <summary>
        /// Primary buffer for storing process network data.
        /// Alternates with MyProcessesBuffer to allow lock-free reading.
        /// </summary>
        public Dictionary<string, MyProcess_Small?> MyProcesses { get; } = new();

        /// <summary>
        /// Secondary buffer for storing process network data.
        /// While GUI reads from MyProcesses, new data goes here (and vice versa).
        /// This double-buffering prevents lock contention with the UI thread.
        /// </summary>
        public Dictionary<string, MyProcess_Small?> MyProcessesBuffer { get; } = new();

        /// <summary>
        /// Buffer for data pending database write. Producers add a whole tick's deltas under its lock;
        /// a DB push copies it out and clears it under the lock and writes outside it. A batch whose write
        /// fails moves to the retry queue (see <c>failedBatches</c>) together with the adapter it belongs to.
        /// </summary>
        public Dictionary<string, MyProcess_Small?> PushToDBBuffer { get; } = new();

        // Held from the end of the background database setup to EndNetworkProcess so the shared SQLite connection
        // stays open between flushes instead of being reopened (and its pragmas re-run) every few seconds. It only
        // holds that connection: PushBatch is told the adapter per call, so it needs no change when the adapter does.
        // Null while the setup is still running; pushes then keep the data buffered. Written under sessionDbLock.
        private volatile ApplicationDB? sessionDb;

        // Serialises publishing/releasing sessionDb against the cancellation of the setup that would publish it, so
        // a stop that races the setup either releases the database or stops it being published; never leaks it.
        private readonly object sessionDbLock = new object();
        private CancellationTokenSource? dbSetupCts;
        private Task? dbSetupTask;

        // A setup attempt that threw is retried by the DB push, at most this often. State below: guarded by sessionDbLock.
        private const long DbSetupRetryIntervalMs = 30_000;
        private bool dbSetupFailed;
        private long dbSetupFailedAtMs;
        private bool dbSetupFailureLogged;   // logged once per failure streak, like a failing write

        // Milliseconds clock for the retry interval; tests replace it.
        private Func<long> clockMilliseconds = () => Environment.TickCount64;

        // Opens the database session; tests replace it to hold the setup at a known point.
        private Func<string, ApplicationDB> createSessionDb = adapter => new ApplicationDB(adapter);

        // True while database writes are failing; the error is logged once per failure streak, not every push.
        private bool dbWriteFailing;

        // Batches whose write failed, by the adapter they were observed on, summed per process name so repeated
        // failures can't grow it beyond (adapters x processes). Each push retries these under their own adapter
        // name before writing the current buffer, so a failure never moves usage to whichever adapter is current
        // later. Survives EndNetworkProcess/StartNetworkProcess; guarded by itself.
        private readonly Dictionary<string, Dictionary<string, (long Received, long Sent)>> failedBatches = new(StringComparer.Ordinal);

        // Where a failed database write is reported. Tests replace it so they don't write to the Windows Event Log.
        internal Action<string, Exception> DbWriteErrorLogger { get; set; } = (message, ex) => EventLogger.Error(message, ex);

        /// <summary>
        /// Controls which buffer receives incoming packet data.
        /// true = write to MyProcessesBuffer, read from MyProcesses
        /// false = write to MyProcesses, read from MyProcessesBuffer
        /// Toggled by the UI layer when extracting data for display.
        /// </summary>
        public bool IsBufferTime { get; set; }

        // Running totals for current session (reset on adapter change)
        public long CurrentSessionDownloadData;
        public long CurrentSessionUploadData;
        public long UploadSpeed;

        //---------- Properties with Change Notification ------------//

        private long downloadSpeed;
        public long DownloadSpeed
        {
            get => downloadSpeed;
            set { downloadSpeed = value; OnPropertyChanged(nameof(DownloadSpeed)); }
        }

        private string isNetworkOnline = "Disconnected";
        /// <summary>
        /// Current connection status. Either "Disconnected" or the adapter name.
        /// </summary>
        public string IsNetworkOnline
        {
            get => isNetworkOnline;
            private set { isNetworkOnline = value; OnPropertyChanged(nameof(IsNetworkOnline)); }
        }

        //---------- Initialization ------------//

        /// <summary>
        /// Call after subscribing to property handlers in MainWindowVM.
        /// Sets up network change monitoring and performs initial connection check.
        /// </summary>
        public void Initialize()
        {
            IsNetworkOnline = "Disconnected";

            // Create the debounce timer (initially not running)
            debounceTimer = new Timer(_ => HandleNetworkChange(), null, Timeout.Infinite, Timeout.Infinite);

            // Subscribe to network address changes (fires on connect/disconnect/IP change)
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

            // Route changes (e.g. a VPN adding its default route) don't always change an address,
            // so they re-trigger the same debounced evaluation.
            routeChangeSubscription = NetworkRoute.SubscribeRouteChanges(() => OnNetworkAddressChanged(null, null));

            // Check current connection state on startup
            HandleNetworkChange();
        }

        //---------- Network Detection (Snapshot-Based) ------------//

        /// <summary>
        /// Queries the system for the current active network connection.
        /// Returns an immutable snapshot of the connection, or null if disconnected.
        /// 
        /// This replaces the old socket-based GetLocalIP() approach which was unreliable
        /// during network transitions (socket connect to 8.8.8.8 would fail transiently).
        /// </summary>
        private NetworkSnapshot? GetCurrentNetworkSnapshot()
        {
            var adapters = EnumerateUpAdapters();
            var candidates = adapters.Select(a => a.Candidate).ToList();

            // Pinned adapter (empty = automatic). A pinned adapter that isn't available
            // yields no snapshot, so the state machine treats it as disconnected.
            var pinnedAdapterId = SettingsManager.Current.NetworkAdapterId;

            // The route lookup only matters for automatic selection.
            int? bestRouteIndex = string.IsNullOrEmpty(pinnedAdapterId)
                ? NetworkRoute.GetBestInterfaceIndex()
                : null;

            var chosen = NetworkAdapterSelector.Select(candidates, pinnedAdapterId, bestRouteIndex);
            if (chosen == null)
                return null; // No valid connection found

            var info = adapters.First(a => ReferenceEquals(a.Candidate, chosen));

            // Build display name (SSID lookup only for the chosen adapter)
            var name = BuildDisplayName(info.Adapter);

            Debug.WriteLine($"{info.Adapter.Name} is up, IP: {(info.IPv4 != null ? new IPAddress(info.IPv4) : new IPAddress(info.IPv6!))}");

            return new NetworkSnapshot(
                name,
                info.Adapter.Id,
                info.IPv4 ?? DefaultIPv4,
                info.IPv6 ?? DefaultIPv6
            );
        }

        /// <summary>
        /// An Up, non-loopback adapter together with the addresses collected from it.
        /// </summary>
        private sealed record UpAdapter(AdapterCandidate Candidate, NetworkInterface Adapter, byte[]? IPv4, byte[]? IPv6);

        /// <summary>
        /// Lists adapters that are Up and not Loopback, with their usable IPv4/IPv6 addresses.
        /// </summary>
        private static List<UpAdapter> EnumerateUpAdapters()
        {
            var result = new List<UpAdapter>();

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip adapters that aren't connected
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                // Skip loopback (127.0.0.1)
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                try
                {
                    var props = adapter.GetIPProperties();

                    byte[]? ipv4 = null;
                    byte[]? ipv6 = null;

                    // Extract assigned IP addresses
                    foreach (var unicast in props.UnicastAddresses)
                    {
                        var addr = unicast.Address;

                        if (addr.AddressFamily == AddressFamily.InterNetwork)
                        {
                            // Skip unspecified and APIPA (169.254.x.x) addresses
                            var bytes = addr.GetAddressBytes();
                            if (NetworkAdapterSelector.IsUsableIPv4(bytes))
                                ipv4 = bytes;
                        }
                        else if (addr.AddressFamily == AddressFamily.InterNetworkV6
                                 && !addr.IsIPv6LinkLocal) // Skip link-local (fe80::) addresses
                        {
                            ipv6 = addr.GetAddressBytes();
                        }
                    }

                    var candidate = new AdapterCandidate(
                        adapter.Id,
                        adapter.Name,
                        GetInterfaceIndex(props),
                        HasGateway: props.GatewayAddresses.Count > 0,
                        HasAddress: ipv4 != null || ipv6 != null);

                    result.Add(new UpAdapter(candidate, adapter, ipv4, ipv6));
                }
                catch (Exception ex)
                {
                    EventLogger.Error($"Failed to read properties of adapter {adapter.Name}", ex);
                }
            }

            return result;
        }

        /// <summary>
        /// Windows interface index of the adapter (IPv4 first, then IPv6), or null if neither
        /// protocol is bound. This is the index GetBestInterface reports.
        /// </summary>
        private static int? GetInterfaceIndex(IPInterfaceProperties props)
        {
            try
            {
                var index = props.GetIPv4Properties()?.Index;
                if (index.HasValue)
                    return index;
            }
            catch (NetworkInformationException)
            {
                // IPv4 isn't bound to this adapter
            }

            try
            {
                return props.GetIPv6Properties()?.Index;
            }
            catch (NetworkInformationException)
            {
                // IPv6 isn't bound to this adapter
            }

            return null;
        }

        /// <summary>
        /// Display name for an adapter: its name, plus the SSID for Wi-Fi.
        /// </summary>
        private static string BuildDisplayName(NetworkInterface adapter)
        {
            var name = adapter.Name;
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                var ssid = GetConnectedSsid(adapter.Id);
                if (!string.IsNullOrEmpty(ssid))
                    name += $"({ssid})";
            }

            return name;
        }

        /// <summary>
        /// Adapters the user can pin in Settings: Up, non-loopback and with a usable address,
        /// sorted by display name. Returns an empty list if enumeration fails.
        /// </summary>
        internal static IReadOnlyList<NetworkAdapterOption> GetSelectableAdapters()
        {
            try
            {
                return EnumerateUpAdapters()
                    .Where(a => a.Candidate.HasAddress)
                    .Select(a => new NetworkAdapterOption(a.Adapter.Id, BuildDisplayName(a.Adapter)))
                    .OrderBy(o => o.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                EventLogger.Error("Failed to list network adapters", ex);
                return Array.Empty<NetworkAdapterOption>();
            }
        }

        // How many recent "connected" events to scan for the requested adapter's SSID.
        private const int MaxWlanEventsToScan = 50;

        /// <summary>
        /// Gets the SSID of the Wi-Fi network the given adapter is connected to by reading
        /// the Windows WLAN AutoConfig event log (most recent connection event for that adapter).
        ///
        /// This is more reliable than the WinRT API on Windows 11 which requires
        /// location permissions to retrieve SSID.
        /// </summary>
        private static string? GetConnectedSsid(string adapterGuid)
        {
            try
            {
                if (!Guid.TryParse(adapterGuid, out Guid wantedGuid))
                    return null;

                // Query for EventID 8001 (successful connection) in WLAN-AutoConfig log
                var query = new EventLogQuery(
                    "Microsoft-Windows-WLAN-AutoConfig/Operational",
                    PathType.LogName,
                    "*[System[EventID=8001]]"
                )
                {
                    ReverseDirection = true // Get most recent first
                };

                using var reader = new EventLogReader(query);
                for (int i = 0; i < MaxWlanEventsToScan; i++)
                {
                    using EventRecord? evt = reader.ReadEvent();
                    if (evt == null)
                        break;

                    // The named event fields are locale-independent, unlike the formatted message.
                    if (TryReadWlanConnection(evt.ToXml(), out Guid interfaceGuid, out string ssid) &&
                        interfaceGuid == wantedGuid)
                        return ssid;
                }
            }
            catch (Exception ex)
            {
                EventLogger.Error("Failed to read connected SSID", ex);
            }

            return null;
        }

        /// <summary>
        /// Reads the interface GUID and SSID from the XML of a WLAN AutoConfig "connected" (8001) event.
        /// Returns false if either field is missing or malformed.
        /// </summary>
        internal static bool TryReadWlanConnection(string eventXml, out Guid interfaceGuid, out string ssid)
        {
            interfaceGuid = Guid.Empty;
            ssid = string.Empty;

            try
            {
                string? guidText = null;
                string? ssidText = null;

                // Match on local names so the event namespace doesn't matter.
                foreach (var data in XDocument.Parse(eventXml).Descendants().Where(e => e.Name.LocalName == "Data"))
                {
                    switch ((string?)data.Attribute("Name"))
                    {
                        case "InterfaceGuid":
                            guidText = data.Value;
                            break;
                        case "SSID":
                            ssidText = data.Value;
                            break;
                    }
                }

                if (!Guid.TryParse(guidText?.Trim(), out Guid parsedGuid) || string.IsNullOrWhiteSpace(ssidText))
                    return false;

                interfaceGuid = parsedGuid;
                ssid = ssidText.Trim();
                return true;
            }
            catch (System.Xml.XmlException)
            {
                return false;
            }
        }

        //---------- Network Change Handling ------------//

        /// <summary>
        /// Re-evaluates the network state right away (no debounce wait), e.g. after the user
        /// changes the adapter selection in Settings. The state machine in HandleNetworkChange
        /// then switches adapters or disconnects as needed.
        /// </summary>
        public void RequestNetworkReevaluation()
        {
            if (isDisposed)
                return;

            try
            {
                debounceTimer?.Change(0, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                EventLogger.Warn("Debounce timer was disposed while requesting network re-evaluation");
            }
        }

        /// <summary>
        /// Event handler for NetworkChange.NetworkAddressChanged.
        /// Resets the debounce timer so that HandleNetworkChange only runs
        /// after DebounceDelayMs of silence (no rapid-fire events).
        /// </summary>
        private void OnNetworkAddressChanged(object? sender, EventArgs? e)
        {
            if (isDisposed)
                return;

            // Reset the timer countdown. If another event arrives before it fires,
            // the timer resets again. No allocations, no CTS, no race conditions.
            try
            {
                debounceTimer?.Change(DebounceDelayMs, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                EventLogger.Warn("Debounce timer was disposed while handling network address change");
            }
        }

        /// <summary>
        /// Core network state machine. Compares current network state to tracked state
        /// and triggers appropriate actions (connect/disconnect/switch/refresh).
        /// 
        /// State transitions:
        /// - Disconnected -> Connected: Start monitoring
        /// - Connected -> Disconnected: Stop monitoring  
        /// - Connected -> Different adapter: Switch in place (the ETW session keeps running, see SwitchAdapter)
        /// - Connected -> Same adapter:
        ///   - IP/name changed: Refresh tracked snapshot (no restart)
        ///   - No meaningful change: Ignore transient event
        /// 
        /// Protected by networkChangeLock to prevent concurrent execution.
        /// </summary>
        private void HandleNetworkChange()
        {
            if (isDisposed)
                return;

            lock (networkChangeLock)
            {
                if (isDisposed)
                    return;

                var snapshot = GetCurrentNetworkSnapshot();

                if (snapshot == null)
                {
                    // No valid connection found
                    if (IsNetworkOnline != "Disconnected")
                    {
                        Debug.WriteLine("Ash: Connection lost");
                        EndNetworkProcess();
                    }
                    return;
                }

                // Valid connection found - determine if it's new or changed
                if (IsNetworkOnline == "Disconnected")
                {
                    // Was disconnected, now connected
                    Debug.WriteLine("Ash: Connection established");
                    ApplySnapshot(snapshot);
                    StartNetworkProcess();
                }
                else if (currentAdapterId != snapshot.AdapterId)
                {
                    // Was connected to different adapter (e.g., switched Wi-Fi networks)
                    // Compare by ID, not name, because SSID retrieval can race with event log
                    Debug.WriteLine("Ash: Network adapter changed");
                    SwitchAdapter(snapshot);
                }
                else
                {
                    // Same adapter can still receive DHCP/IPv6 address changes or an SSID/name update.
                    // Refresh tracked snapshot so packet filtering keeps matching local traffic.
                    bool ipChanged = HasSnapshotAddressChanged(snapshot);
                    bool nameChanged = !string.Equals(AdapterName, snapshot.AdapterName, StringComparison.Ordinal);
                    if (ipChanged || nameChanged)
                    {
                        Debug.WriteLine("Ash: Adapter IP/name changed on same adapter - refreshing snapshot");
                        ApplySnapshot(snapshot);

                        // Keep status text in sync when SSID/name changes without adapter GUID change.
                        if (nameChanged)
                            IsNetworkOnline = AdapterName;
                    }
                }
            }
        }

        private bool HasSnapshotAddressChanged(NetworkSnapshot snapshot)
        {
            lock (stateLock)
            {
                return !ByteArray.Compare(localIPv4, snapshot.IPv4) ||
                       !ByteArray.Compare(localIPv6, snapshot.IPv6);
            }
        }

        /// <summary>
        /// Updates tracked state from a network snapshot.
        /// Called when establishing a new connection, switching adapters,
        /// or refreshing state on same-adapter IP/name changes.
        /// </summary>
        private void ApplySnapshot(NetworkSnapshot snapshot)
        {
            // One critical section: the packet filter and the adapter's identity change together.
            lock (stateLock)
            {
                localIPv4 = snapshot.IPv4;
                localIPv6 = snapshot.IPv6;
                currentAdapterId = snapshot.AdapterId;
                AdapterName = snapshot.AdapterName;
            }
        }

        //---------- Start/Stop Network Monitoring ------------//

        /// <summary>
        /// Starts all network monitoring components:
        /// - ETW packet capture
        /// - Speed monitoring periodic task
        /// - Database push periodic task
        /// - Database setup, in the background
        /// Capture comes first and nothing waits for the database: until its session exists a push returns early and
        /// the traffic stays buffered.
        /// </summary>
        public void StartNetworkProcess()
        {
            // Start packet capture and periodic tasks
            CaptureNetworkPackets();
            StartSpeedMonitoring();
            StartDbPush();
            StartDatabaseSetup(AdapterName);

            // Update connection status (triggers UI update via PropertyChanged)
            IsNetworkOnline = AdapterName;
        }

        /// <summary>
        /// Stops all network monitoring components and resets state.
        /// Called on disconnect and on exit; an adapter switch uses <see cref="SwitchAdapter"/> instead.
        /// </summary>
        public void EndNetworkProcess()
        {
            // Stop ETW session first (generates most activity)
            StopKernelSession();

            // Stop periodic tasks
            StopPeriodicWork(ref networkSpeedWork, "Network speed");
            StopPeriodicWork(ref dbPushWork, "DB push");

            // Nothing counts or ticks any more: hand over the last partial second (counted since the final tick) to
            // the UI and the database buffer, under the current adapter, so the final flush below writes it. It runs
            // after the stops on purpose: stopping the speed work waits for an in-flight tick, and that tick needs
            // tickLock, so this must not hold it earlier; and the stopped session can't add anything behind it.
            lock (tickLock)
                RequestTrafficFlush();

            // From here no setup may publish a session; one that already did is flushed to and released below.
            CancelDatabaseSetup();

            FlushPendingDbWrites();

            // Last flush is done: release the session's reference so the connection closes (and SQLite checkpoints
            // the WAL) when nothing else is using it.
            ReleaseSessionDb();

            // Clear process data buffers
            lock (MyProcesses) MyProcesses.Clear();
            lock (MyProcessesBuffer) MyProcessesBuffer.Clear();

            // Reset speed counters
            lock (tickLock)
                ResetSessionCounters();

            // Reset adapter tracking
            currentAdapterId = "";
            IsNetworkOnline = "Disconnected";
        }

        /// <summary>
        /// Moves monitoring to another adapter without stopping capture: the ETW session, the periodic tasks and
        /// the database session all keep running. The session only filters by local IP, so the switch is a change
        /// of that filter plus a clean hand-over of what the old adapter counted. Runs under networkChangeLock.
        ///
        /// Everything happens under tickLock, so the speed tick can't run in between:
        /// (a) the old adapter's tail (counted since the last tick) is flushed to the UI and staged for the
        ///     database, and everything staged is taken out together with the old adapter's name;
        /// (b) the new snapshot (local IPs, adapter id, name) is applied atomically;
        /// (c) the session counters and the speed tick's baseline restart at zero, so the first tick afterwards
        ///     doesn't see the old totals and report a negative speed;
        /// (d) the status goes "Disconnected" then to the new name, so the UI clears the old adapter's session
        ///     and rebases on the new one exactly as it did when a switch restarted capture.
        /// Afterwards, outside tickLock, (e) the tail is written under the OLD adapter's name and the new adapter's
        /// row is made sure of. A packet counted between (a) and (b) is the only thing that can end up on the wrong
        /// adapter, and only by the few microseconds that gap lasts.
        /// </summary>
        private void SwitchAdapter(NetworkSnapshot snapshot)
        {
            string oldAdapter;
            List<(string Name, long Received, long Sent)> tail;

            lock (tickLock)
            {
                // (a)
                RequestTrafficFlush();
                (oldAdapter, tail) = TakePending();

                // (b)
                ApplySnapshot(snapshot);

                // (c)
                ResetSessionCounters();

                // (d)
                IsNetworkOnline = "Disconnected";
                IsNetworkOnline = AdapterName;
            }

            // (e)
            WriteSwitchTail(oldAdapter, tail);
            EnsureAdapterRow(snapshot.AdapterName);
        }

        /// <summary>
        /// Has the owner hand over what was counted since the last tick (see <see cref="TrafficFlushRequested"/>).
        /// A subscriber that throws must not derail the switch or the stop that asked: the flush stages the traffic
        /// for the database before it raises anything, so that part has happened by then.
        /// </summary>
        private void RequestTrafficFlush()
        {
            try
            {
                TrafficFlushRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorLogger("Failed to hand over the traffic counted since the last tick", ex);
            }
        }

        /// <summary>
        /// Zeroes the session totals and speeds and the speed tick's baseline. Caller holds tickLock.
        /// </summary>
        private void ResetSessionCounters()
        {
            Interlocked.Exchange(ref CurrentSessionDownloadData, 0);
            Interlocked.Exchange(ref CurrentSessionUploadData, 0);
            speedBaselineDownload = 0;
            speedBaselineUpload = 0;

            // The backing field, not the property: assigning DownloadSpeed announces a tick.
            downloadSpeed = 0;
            UploadSpeed = 0;
        }

        /// <summary>
        /// Stops the ETW kernel session and waits for the capture task to complete.
        /// </summary>
        private void StopKernelSession()
        {
            IKernelCaptureSession? session;
            Task? task;

            lock (captureLock)
            {
                // Under the lock so the health check can't restart the session that is being stopped.
                captureWanted = false;
                session = kernelSession;
                kernelSession = null;
                task = PacketTask;
            }

            // Disposing the session causes Source.Process() to return
            session?.Dispose();

            if (task != null)
            {
                try
                {
                    task.Wait(TimeSpan.FromMilliseconds(OneSec));
                }
                catch (AggregateException ex)
                {
                    ErrorLogger("Packet capture stop error", ex);
                }
                finally
                {
                    if (task.IsCompleted)
                        task.Dispose();
                    else
                        task.ContinueWith(t => t.Dispose(), TaskScheduler.Default);

                    lock (captureLock)
                        PacketTask = null;
                }
            }
        }

        /// <summary>
        /// Safely stops a periodic work task with error handling.
        /// </summary>
        private void StopPeriodicWork(ref PeriodicWork? work, string name)
        {
            try
            {
                work?.Stop();
            }
            catch (Exception ex)
            {
                EventLogger.Error($"{name} stop error", ex);
            }
            finally
            {
                work = null;
            }
        }

        /// <summary>
        /// Creates the tables, the adapter row and today's rows and opens the session connection on a worker thread,
        /// so starting capture doesn't wait for SQLite. The finished session is published to <see cref="sessionDb"/>
        /// only if the setup wasn't cancelled meanwhile (see <see cref="CancelDatabaseSetup"/>). A setup that throws
        /// is retried by <see cref="PushPendingToDb"/> (<paramref name="retry"/>), at most every
        /// <see cref="DbSetupRetryIntervalMs"/>, for as long as the session lasts.
        /// </summary>
        private void StartDatabaseSetup(string adapter, bool retry = false)
        {
            CancellationTokenSource cts;
            ApplicationDB? leftover;

            lock (sessionDbLock)
            {
                // Checked under the same lock that starts the attempt, so a stop (which clears dbSetupCts under it)
                // can never be followed by a retry that nobody would cancel.
                if (retry && (!dbSetupFailed || dbSetupCts == null ||
                              clockMilliseconds() - dbSetupFailedAtMs < DbSetupRetryIntervalMs))
                    return;

                // A start without a stop in between must not leave the earlier session behind.
                dbSetupCts?.Cancel();
                cts = new CancellationTokenSource();
                dbSetupCts = cts;
                leftover = sessionDb;
                sessionDb = null;
                dbSetupFailed = false;
                if (!retry)
                    dbSetupFailureLogged = false;
            }

            leftover?.Dispose();

            var token = cts.Token;
            dbSetupTask = Task.Run(() => SetUpDatabase(adapter, token));
        }

        private void SetUpDatabase(string adapter, CancellationToken token)
        {
            ApplicationDB? db = null;

            try
            {
                if (token.IsCancellationRequested)
                    return;

                // Ensure database table exists for this adapter. The instance is kept for the whole session (it is
                // disposed in EndNetworkProcess) so the connection it holds stays open between flushes.
                db = createSessionDb(adapter);
                if (db.CreateTable() < 0)
                {
                    ErrorLogger("Error: Create table", null);
                }
                else
                {
                    db.InsertUniqueRow_AdapterTable(adapter);
                    db.UpdateDatesInDB();
                }

                lock (sessionDbLock)
                {
                    // Stopped (or restarted) while this ran: nobody will release the session, so don't publish it.
                    // The finally below disposes it.
                    if (token.IsCancellationRequested)
                        return;

                    sessionDb = db;
                    db = null;
                    dbSetupFailed = false;
                    dbSetupFailureLogged = false;
                }

                // The adapter may have switched while the setup ran; the switch found no session to prepare.
                var current = AdapterName;
                if (!string.Equals(current, adapter, StringComparison.Ordinal))
                    EnsureAdapterRow(current);
            }
            catch (Exception ex)
            {
                bool log;
                lock (sessionDbLock)
                {
                    // A cancelled attempt (stopped, or replaced by a newer one) has nothing left to retry.
                    if (token.IsCancellationRequested)
                        return;

                    // Pushes keep the data buffered or queued until a retry gets a session.
                    dbSetupFailed = true;
                    dbSetupFailedAtMs = clockMilliseconds();
                    log = !dbSetupFailureLogged;
                    dbSetupFailureLogged = true;
                }

                if (log)
                    ErrorLogger("Database setup error; usage stays buffered and the setup is retried every 30 seconds", ex);
            }
            finally
            {
                db?.Dispose();
            }
        }

        /// <summary>
        /// Stops a running database setup (or a pending retry) from publishing its session. A session published
        /// before this call is not touched; <see cref="ReleaseSessionDb"/> disposes it. Both take sessionDbLock, so
        /// a setup that races a stop either published first (and is released) or finds itself cancelled (and
        /// disposes its own session).
        /// </summary>
        private void CancelDatabaseSetup()
        {
            lock (sessionDbLock)
            {
                dbSetupCts?.Cancel();
                dbSetupCts = null;
                dbSetupFailed = false;
            }
        }

        private void ReleaseSessionDb()
        {
            ApplicationDB? db;
            lock (sessionDbLock)
            {
                db = sessionDb;
                sessionDb = null;
            }

            db?.Dispose();
        }

        /// <summary>Makes sure the adapter has its row (History lists adapters from it) and today's Date row exists.</summary>
        private void EnsureAdapterRow(string adapter)
        {
            var db = sessionDb;
            if (db == null)
                return;

            try
            {
                db.InsertUniqueRow_AdapterTable(adapter);
                db.UpdateDatesInDB();
            }
            catch (Exception ex)
            {
                ErrorLogger("Failed to add the adapter to the database", ex);
            }
        }

        private void FlushPendingDbWrites()
        {
            if (string.IsNullOrWhiteSpace(AdapterName))
            {
                lock (PushToDBBuffer)
                    PushToDBBuffer.Clear();
                return;
            }

            if (sessionDb == null)
            {
                // Stopped before the database setup finished: keep what was counted, with its adapter, for the next
                // session's first push instead of dropping it.
                var (adapter, rows) = TakePending();
                if (rows.Count > 0)
                    QueueFailedBatch(adapter, rows);
                return;
            }

            PushPendingToDb();
        }

        /// <summary>
        /// Writes the old adapter's tail (see <see cref="SwitchAdapter"/>) under that adapter's name. Without a
        /// database session yet it is queued with its adapter, like a failed write, for the first push.
        /// </summary>
        private void WriteSwitchTail(string adapter, List<(string Name, long Received, long Sent)> rows)
        {
            if (rows.Count == 0)
                return;

            var db = sessionDb;
            if (db == null)
                QueueFailedBatch(adapter, rows);
            else
                TryWriteBatch(db, adapter, rows);
        }

        /// <summary>
        /// Writes usage to the database: first the batches that failed earlier, each under the adapter it was observed
        /// on, then everything staged in <see cref="PushToDBBuffer"/> under the adapter that was current when it was
        /// taken. Each batch is one transaction. The buffer is copied out and cleared under its lock and written
        /// outside it, so a slow write never delays the per-second staging of traffic. A batch that fails is queued
        /// for the next push.
        /// With no database session (between sessions, or while the setup still runs) nothing is written, the
        /// staged traffic stays buffered and the queue simply waits.
        /// </summary>
        private void PushPendingToDb()
        {
            var db = sessionDb;
            if (db == null)
            {
                // No session: the traffic stays buffered. If the setup failed, this is also where it is retried.
                StartDatabaseSetup(AdapterName, retry: true);
                return;
            }

            bool attempted = false;
            bool failed = false;

            foreach (var (adapter, rows) in TakeFailedBatches())
            {
                attempted = true;
                failed |= !TryWriteBatch(db, adapter, rows);
            }

            var (currentAdapter, batch) = TakePending();
            if (batch.Count > 0)
            {
                attempted = true;
                failed |= !TryWriteBatch(db, currentAdapter, batch);
            }

            if (attempted && !failed)
                dbWriteFailing = false;
        }

        /// <summary>
        /// Copies <see cref="PushToDBBuffer"/> out and clears it, together with the adapter it belongs to. The name is
        /// read under the same lock: the buffer is only filled under tickLock and an adapter switch takes it out
        /// before changing the name, so whatever is in the buffer was counted on the adapter named here.
        /// </summary>
        private (string Adapter, List<(string Name, long Received, long Sent)> Rows) TakePending()
        {
            lock (PushToDBBuffer)
            {
                var batch = new List<(string Name, long Received, long Sent)>(PushToDBBuffer.Count);
                foreach (var (name, usage) in PushToDBBuffer)
                {
                    if (usage != null)
                        batch.Add((name, usage.CurrentDataRecv, usage.CurrentDataSend));
                }

                PushToDBBuffer.Clear();
                return (AdapterName, batch);
            }
        }

        /// <summary>Writes one batch; on failure queues it under <paramref name="adapter"/> and logs once per failure streak.</summary>
        private bool TryWriteBatch(ApplicationDB db, string adapter, IReadOnlyCollection<(string Name, long Received, long Sent)> batch)
        {
            try
            {
                db.PushBatch(adapter, batch);
                return true;
            }
            catch (Exception ex)
            {
                QueueFailedBatch(adapter, batch);

                if (!dbWriteFailing)
                {
                    dbWriteFailing = true;
                    DbWriteErrorLogger("Failed to write network usage to the database; it is kept with its adapter and retried on the next push", ex);
                }

                return false;
            }
        }

        private void QueueFailedBatch(string adapter, IEnumerable<(string Name, long Received, long Sent)> batch)
        {
            lock (failedBatches)
            {
                if (!failedBatches.TryGetValue(adapter, out var queued))
                {
                    queued = new Dictionary<string, (long Received, long Sent)>(StringComparer.Ordinal);
                    failedBatches[adapter] = queued;
                }

                foreach (var (name, received, sent) in batch)
                {
                    queued.TryGetValue(name, out var existing);
                    queued[name] = (existing.Received + received, existing.Sent + sent);
                }
            }
        }

        /// <summary>
        /// Empties the retry queue and returns it as batches. Entries that fail again are queued anew, so nothing
        /// is lost if another thread queues a failure while these are being written.
        /// </summary>
        private List<(string Adapter, List<(string Name, long Received, long Sent)> Rows)> TakeFailedBatches()
        {
            var taken = new List<(string Adapter, List<(string Name, long Received, long Sent)> Rows)>();
            lock (failedBatches)
            {
                foreach (var (adapter, queued) in failedBatches)
                {
                    var rows = new List<(string Name, long Received, long Sent)>(queued.Count);
                    foreach (var (name, usage) in queued)
                        rows.Add((name, usage.Received, usage.Sent));

                    taken.Add((adapter, rows));
                }

                failedBatches.Clear();
            }

            return taken;
        }

        //---------- Periodic Tasks ------------//

        /// <summary>
        /// Starts the periodic task that calculates current network speed.
        /// Runs every second and computes speed as delta from previous totals. Every tick also raises the traffic
        /// batch (the DownloadSpeed assignment is the announcement), which makes this the app's one-second clock.
        /// </summary>
        private void StartSpeedMonitoring()
        {
            networkSpeedWork = new PeriodicWork("Network speed", TimeSpan.FromSeconds(1));
            networkSpeedWork.Start(_ =>
            {
                SpeedTick();
                return Task.CompletedTask;
            });
        }

        private void SpeedTick()
        {
            // Under tickLock so an adapter switch can't reset the counters and baseline halfway through a tick.
            lock (tickLock)
            {
                // Read current totals (atomic reads)
                long currentDown = Interlocked.Read(ref CurrentSessionDownloadData);
                long currentUp = Interlocked.Read(ref CurrentSessionUploadData);

                long downloadDelta = currentDown - speedBaselineDownload;
                long uploadDelta = currentUp - speedBaselineUpload;

                // Store for next iteration's delta calculation. Before the tick is announced, so a subscriber that
                // throws can't make the next tick count this second again.
                speedBaselineDownload = currentDown;
                speedBaselineUpload = currentUp;

                // Calculate speed based on user's preferred format: bits per second, or bytes per second
                long unit = SettingsManager.Current.NetworkSpeedFormat == 0 ? 8 : 1;
                UploadSpeed = uploadDelta * unit;
                DownloadSpeed = downloadDelta * unit;
            }

            // After the batch has gone out, so a (rare, slow) session restart never delays it.
            CheckCaptureHealth();
        }

        /// <summary>
        /// Starts the periodic task that pushes accumulated process data to the database.
        /// Runs every 5 seconds to batch writes and reduce I/O.
        /// </summary>
        private void StartDbPush()
        {
            dbPushWork = new PeriodicWork("DB push", TimeSpan.FromSeconds(5));
            dbPushWork.Start(_ =>
            {
#if DEBUG
                var sw = Stopwatch.StartNew();
#endif
                PushPendingToDb();
#if DEBUG
                sw.Stop();
                Debug.WriteLine($"elapsed time (DBpush): {sw.ElapsedMilliseconds} | time {DateTime.Now:O}");
#endif
                return Task.CompletedTask;
            });
        }

        //---------- ETW Packet Capture ------------//

        /// <summary>
        /// Starts the ETW (Event Tracing for Windows) kernel session to capture
        /// all TCP/IP network packets on the system.
        /// 
        /// Uses the NT Kernel Logger session which requires admin privileges.
        /// Packets are filtered to only count those matching our local IP.
        /// </summary>
        private void CaptureNetworkPackets()
        {
            lock (captureLock)
            {
                captureWanted = true;
                captureRestartCooldown = 0;
                captureHealthyTicks = 0;
                captureFailureLogged = false;
                StartKernelSessionLocked();
            }
        }

        /// <summary>
        /// Creates the kernel session on the calling thread and starts the task that consumes it. The session is
        /// created and enabled synchronously, so when this returns tracing has started and kernelSession is set;
        /// the task only runs <see cref="IKernelCaptureSession.Process"/>. Previously the session was created inside
        /// the task, so a stop that came right after the start could miss it and leave the session running.
        /// Caller holds captureLock.
        /// </summary>
        private void StartKernelSessionLocked()
        {
            if (kernelSession != null)
                return;

            IKernelCaptureSession session;
            try
            {
                // Create kernel trace session (requires admin)
                session = kernelCaptureFactory(ProcessPacket);
            }
            catch (Exception ex)
            {
                // Logged once per failure streak; the health check keeps retrying quietly.
                if (!captureFailureLogged)
                {
                    captureFailureLogged = true;
                    ErrorLogger("Packet capture start error", ex);
                }

                return;
            }

            kernelSession = session;
            PacketTask = Task.Run(() => RunCapture(session));
        }

        private void RunCapture(IKernelCaptureSession session)
        {
            try
            {
                // Blocks until session is disposed
                session.Process();
            }
            catch (Exception ex)
            {
                ErrorLogger("Packet capture loop error", ex);
            }
        }

        /// <summary>
        /// Runs every tick. Switching adapters no longer restarts capture, so nothing else would notice a capture
        /// task that ended on its own (the session was stopped from outside, or the loop failed): if the capture
        /// task is gone while capture is wanted, log it once and restart the kernel session. Restarts are at most
        /// <see cref="CaptureRestartCooldownTicks"/> seconds apart and a failure streak is logged only once.
        /// </summary>
        private void CheckCaptureHealth()
        {
            lock (captureLock)
            {
                if (!captureWanted)
                    return;

                if (captureRestartCooldown > 0)
                    captureRestartCooldown--;

                var task = PacketTask;
                if (task is { IsCompleted: false })
                {
                    // Running. Once it has for a while the streak is over, so a later failure is logged again.
                    if (++captureHealthyTicks >= CaptureHealthyTicksToRearmLog)
                    {
                        captureHealthyTicks = 0;
                        captureFailureLogged = false;
                    }

                    return;
                }

                captureHealthyTicks = 0;
                if (captureRestartCooldown > 0)
                    return;

                captureRestartCooldown = CaptureRestartCooldownTicks;

                if (!captureFailureLogged)
                {
                    captureFailureLogged = true;
                    ErrorLogger("Packet capture stopped unexpectedly; restarting the kernel session", null);
                }

                try
                {
                    // The dead session still owns the session name; let go of it before creating the next one.
                    kernelSession?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Disposing the stopped kernel session failed: {ex.Message}");
                }

                kernelSession = null;
                if (task is { IsCompleted: true })
                    task.Dispose();
                PacketTask = null;

                StartKernelSessionLocked();
            }
        }

        /// <summary>
        /// Processes a single network packet captured by ETW.
        /// Filters packets to only count those involving our local IP,
        /// then records to the appropriate buffer based on direction.
        /// </summary>
        /// <param name="src">Source IP address</param>
        /// <param name="dest">Destination IP address</param>
        /// <param name="size">Packet size in bytes</param>
        /// <param name="processName">Name of the process that sent/received the packet</param>
        /// <param name="isRecv">True for download, false for upload</param>
        private void ProcessPacket(IPAddress src, IPAddress dest, int size, string processName, bool isRecv)
        {
            // Get current local IP for this address family
            byte[] localIp;
            lock (stateLock)
            {
                localIp = src.AddressFamily == AddressFamily.InterNetwork ? localIPv4 : localIPv6;
            }

            // Compare source/destination against the local IP without allocating
            // byte arrays for every packet. During sustained high-throughput
            // copies this significantly reduces GC pressure.
            bool isSrc = MatchesLocalIp(src, localIp);
            bool isDest = MatchesLocalIp(dest, localIp);

            // XOR: exactly one should match (either we sent it or received it)
            // If both match (loopback) or neither match (other adapter), skip
            if (!(isSrc ^ isDest))
                return;

            // Apply network type filter (private/public/both)
            if (!ShouldProcessByNetworkType(isSrc, src, dest))
                return;

            // Record the packet to appropriate counter and buffer
            if (isRecv)
                RecordRecv(processName, size);
            else
                RecordSend(processName, size);
        }

        /// <summary>
        /// Checks if a packet should be counted based on the user's network type filter setting.
        /// </summary>
        /// <param name="isLocalSrc">True if local IP is the source (upload), false if destination (download)</param>
        /// <param name="src">Source IP</param>
        /// <param name="dest">Destination IP</param>
        /// <returns>True if packet should be counted</returns>
        private bool ShouldProcessByNetworkType(bool isLocalSrc, IPAddress src, IPAddress dest)
        {
            // Remote IP is the one that isn't ours
            var remoteIp = isLocalSrc ? dest : src;

            return SettingsManager.Current.NetworkType switch
            {
                0 => IsPrivateIP(remoteIp),   // Private only (LAN traffic)
                1 => !IsPrivateIP(remoteIp),  // Public only (internet traffic)
                2 => true,                     // Both
                _ => false
            };
        }

        /// <summary>
        /// Records a received (download) packet.
        /// </summary>
        private void RecordRecv(string name, int size)
        {
            // Thread-safe increment of running total
            Interlocked.Add(ref CurrentSessionDownloadData, size);
            RecordToBuffer(name, size, isRecv: true);
        }

        /// <summary>
        /// Records a sent (upload) packet.
        /// </summary>
        private void RecordSend(string name, int size)
        {
            // Thread-safe increment of running total
            Interlocked.Add(ref CurrentSessionUploadData, size);
            RecordToBuffer(name, size, isRecv: false);
        }

        /// <summary>
        /// Records packet data to the active process buffer.
        /// Uses double-buffering to minimize lock contention with UI reads.
        /// </summary>
        private void RecordToBuffer(string name, int size, bool isRecv)
        {
            // Empty process name means kernel/system traffic
            if (string.IsNullOrEmpty(name))
                name = "System";

            // Select buffer based on current phase
            var dict = IsBufferTime ? MyProcessesBuffer : MyProcesses;

            lock (dict)
            {
                // Get or create process entry (single lookup)
                if (!dict.TryGetValue(name, out var proc))
                {
                    proc = new MyProcess_Small(name, 0, 0);
                    dict[name] = proc;
                }

                // Accumulate data
                if (isRecv)
                    proc!.CurrentDataRecv += size;
                else
                    proc!.CurrentDataSend += size;
            }
        }

        //---------- IP Classification ------------//

        /// <summary>
        /// Compares an IP address to the cached local IP bytes without allocating
        /// a new byte array. Uses a stack-allocated span as temporary storage.
        /// </summary>
        private static bool MatchesLocalIp(IPAddress ip, byte[] localIpBytes)
        {
            Span<byte> temp = stackalloc byte[16];
            if (!ip.TryWriteBytes(temp.Slice(0, localIpBytes.Length), out _))
                return false;

            return temp.Slice(0, localIpBytes.Length).SequenceEqual(localIpBytes.AsSpan());
        }

        /// <summary>
        /// Determines if an IP address is in a private (non-routable) range.
        /// Used for filtering traffic by network type (LAN vs internet).
        /// 
        /// Private ranges:
        /// - IPv4: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16
        /// - IPv6: Link-local (fe80::), site-local, unique-local (fc00::/7)
        /// </summary>
        private bool IsPrivateIP(IPAddress ip)
        {
            // Handle IPv4-mapped IPv6 addresses (::ffff:x.x.x.x)
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            // Loopback is always private
            if (IPAddress.IsLoopback(ip))
                return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = ip.GetAddressBytes();
                return bytes[0] == 10 ||                                        // 10.0.0.0/8 (Class A)
                       (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || // 172.16.0.0/12 (Class B)
                       (bytes[0] == 192 && bytes[1] == 168) ||                  // 192.168.0.0/16 (Class C)
                       (bytes[0] == 169 && bytes[1] == 254);                    // 169.254.0.0/16 (link-local/APIPA)
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal ||     // fe80::/10
#if NET6_0_OR_GREATER
                       ip.IsIPv6UniqueLocal ||   // fc00::/7 (only available in .NET 6+)
#endif
                       ip.IsIPv6SiteLocal;       // fec0::/10 (deprecated but still checked)
            }

            return false;
        }

        //---------- Property Changed ------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        //---------- Dispose ------------//

        /// <summary>
        /// Cleans up all resources: unsubscribes from events, stops monitoring,
        /// and flushes any pending data to the database.
        /// </summary>
        public void Dispose()
        {
            isDisposed = true;

            // Unsubscribe from network change events
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            routeChangeSubscription?.Dispose();
            routeChangeSubscription = null;

            // Dispose the debounce timer (prevents any pending callback from firing)
            debounceTimer?.Dispose();
            debounceTimer = null;

            // Stop monitoring if active. An adapter switch reports "Disconnected" for a moment while capture keeps
            // running, so the capture flag counts too.
            if (IsNetworkOnline != "Disconnected" || captureWanted)
                EndNetworkProcess();

            // Flush any remaining buffered data to database
            FlushPendingDbWrites();

            // Normally released by EndNetworkProcess; this covers a start that failed before the network came online.
            CancelDatabaseSetup();
            ReleaseSessionDb();
        }
    }
}
