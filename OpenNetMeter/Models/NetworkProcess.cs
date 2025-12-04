using System;
using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using OpenNetMeter.Utilities;
using OpenNetMeter.Properties;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace OpenNetMeter.Models
{
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

        //---------- Debounce & Synchronization ------------//

        // Lock for debounce cancellation token access
        private readonly object debounceLock = new object();
        private CancellationTokenSource? networkChangeDebounce;

        // Lock to ensure only one HandleNetworkChange runs at a time
        private readonly object networkChangeLock = new object();

        //---------- Periodic Tasks ------------//

        // Periodic task for updating network speed display
        private PeriodicWork? networkSpeedWork;

        // Periodic task for pushing process data to database
        private PeriodicWork? dbPushWork;

        //---------- ETW Session ------------//

        // ETW kernel session for capturing network packets
        private TraceEventSession? kernelSession;

        // Task running the ETW packet capture loop
        public Task? PacketTask;

        //---------- Public State ------------//

        // Current network adapter name (includes SSID for Wi-Fi connections)
        public string AdapterName { get; private set; } = "";

        // Current adapter's unique ID (GUID) - used for comparison to detect changes
        private string currentAdapterId = "";

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
        /// Buffer for data pending database write.
        /// Cleared after each successful DB push.
        /// </summary>
        public Dictionary<string, MyProcess_Small?> PushToDBBuffer { get; } = new();

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

            // Subscribe to network address changes (fires on connect/disconnect/IP change)
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

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
            var adapters = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var adapter in adapters)
            {
                // Skip adapters that aren't connected
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                // Skip loopback (127.0.0.1)
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var props = adapter.GetIPProperties();

                // No gateway = not a real internet connection (e.g., virtual adapters)
                if (props.GatewayAddresses.Count == 0)
                    continue;

                byte[]? ipv4 = null;
                byte[]? ipv6 = null;

                // Extract assigned IP addresses
                foreach (var unicast in props.UnicastAddresses)
                {
                    var addr = unicast.Address;

                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4 = addr.GetAddressBytes();
                    }
                    else if (addr.AddressFamily == AddressFamily.InterNetworkV6
                             && !addr.IsIPv6LinkLocal) // Skip link-local (fe80::) addresses
                    {
                        ipv6 = addr.GetAddressBytes();
                    }
                }

                // Need at least one usable IP to consider this a valid connection
                if (ipv4 == null && ipv6 == null)
                    continue;

                // Build display name (include SSID for Wi-Fi)
                var name = adapter.Name;
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    var ssid = GetConnectedSsid(adapter.Id);
                    if (!string.IsNullOrEmpty(ssid))
                        name += $"({ssid})";
                }

                Debug.WriteLine($"{adapter.Name} is up, IP: {(ipv4 != null ? new IPAddress(ipv4) : new IPAddress(ipv6!))}");

                return new NetworkSnapshot(
                    name,
                    adapter.Id,
                    ipv4 ?? DefaultIPv4,
                    ipv6 ?? DefaultIPv6
                );
            }

            return null; // No valid connection found
        }

        /// <summary>
        /// Gets the SSID of the currently connected Wi-Fi network by reading
        /// from the Windows WLAN AutoConfig event log.
        /// 
        /// This is more reliable than the WinRT API on Windows 11 which requires
        /// location permissions to retrieve SSID.
        /// </summary>
        private static string? GetConnectedSsid(string adapterGuid)
        {
            try
            {
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
                if (reader.ReadEvent() is EventRecord evt)
                {
                    var message = evt.FormatDescription();
                    var match = Regex.Match(message, @"^SSID:\s*(.+)$", RegexOptions.Multiline);
                    if (match.Success)
                        return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Silently fail - SSID is nice-to-have, not critical
            }

            return null;
        }

        //---------- Network Change Handling ------------//

        /// <summary>
        /// Event handler for NetworkChange.NetworkAddressChanged.
        /// Debounces rapid events (common during network transitions) before processing.
        /// </summary>
        private void OnNetworkAddressChanged(object? sender, EventArgs? e)
        {
            CancellationTokenSource cts;

            lock (debounceLock)
            {
                // Cancel any pending debounce timer
                networkChangeDebounce?.Cancel();
                networkChangeDebounce?.Dispose();
                networkChangeDebounce = new CancellationTokenSource();
                cts = networkChangeDebounce;
            }

            // Wait for debounce period, then process if not cancelled
            Task.Delay(DebounceDelayMs, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    HandleNetworkChange();
            }, TaskScheduler.Default);
        }

        /// <summary>
        /// Core network state machine. Compares current network state to tracked state
        /// and triggers appropriate actions (connect/disconnect/switch).
        /// 
        /// State transitions:
        /// - Disconnected -> Connected: Start monitoring
        /// - Connected -> Disconnected: Stop monitoring  
        /// - Connected -> Different adapter: Stop then start monitoring
        /// - Connected -> Same adapter: No action (ignore transient events)
        /// 
        /// Protected by networkChangeLock to prevent concurrent execution.
        /// </summary>
        private void HandleNetworkChange()
        {
            lock (networkChangeLock)
            {
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
                    EndNetworkProcess();
                    ApplySnapshot(snapshot);
                    StartNetworkProcess();
                }
                // else: Same adapter, already connected - nothing to do
                // This handles transient events during stable connections
            }
        }

        /// <summary>
        /// Updates tracked state from a network snapshot.
        /// Called when establishing a new connection or switching adapters.
        /// </summary>
        private void ApplySnapshot(NetworkSnapshot snapshot)
        {
            lock (stateLock)
            {
                localIPv4 = snapshot.IPv4;
                localIPv6 = snapshot.IPv6;
            }
            currentAdapterId = snapshot.AdapterId;
            AdapterName = snapshot.AdapterName;
        }

        //---------- Start/Stop Network Monitoring ------------//

        /// <summary>
        /// Starts all network monitoring components:
        /// - Database table setup
        /// - ETW packet capture
        /// - Speed monitoring periodic task
        /// - Database push periodic task
        /// </summary>
        public void StartNetworkProcess()
        {
            // Ensure database table exists for this adapter
            using (var db = new ApplicationDB(AdapterName))
            {
                if (db.CreateTable() < 0)
                {
                    Debug.WriteLine("Error: Create table");
                }
                else
                {
                    Debug.WriteLine($"Table created or already exists, adapter table: {AdapterName}");
                    db.InsertUniqueRow_AdapterTable(AdapterName);
                    db.UpdateDatesInDB();
                }
            }

            // Start packet capture and periodic tasks
            CaptureNetworkPackets();
            StartSpeedMonitoring();
            StartDbPush();

            // Update connection status (triggers UI update via PropertyChanged)
            IsNetworkOnline = AdapterName;
        }

        /// <summary>
        /// Stops all network monitoring components and resets state.
        /// Called on disconnect or before switching to a different adapter.
        /// </summary>
        public void EndNetworkProcess()
        {
            // Stop ETW session first (generates most activity)
            StopKernelSession();

            // Stop periodic tasks
            StopPeriodicWork(ref networkSpeedWork, "Network speed");
            StopPeriodicWork(ref dbPushWork, "DB push");

            // Clear process data buffers
            lock (MyProcesses) MyProcesses.Clear();
            lock (MyProcessesBuffer) MyProcessesBuffer.Clear();

            // Reset speed counters
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            UploadSpeed = 0;
            DownloadSpeed = 0;

            // Reset adapter tracking
            currentAdapterId = "";
            IsNetworkOnline = "Disconnected";
        }

        /// <summary>
        /// Stops the ETW kernel session and waits for the capture task to complete.
        /// </summary>
        private void StopKernelSession()
        {
            // Disposing the session causes Source.Process() to return
            kernelSession?.Dispose();
            kernelSession = null;

            var task = PacketTask;
            if (task != null)
            {
                try
                {
                    task.Wait(TimeSpan.FromMilliseconds(OneSec));
                }
                catch (AggregateException ex)
                {
                    EventLogger.Error($"Packet capture stop error: {ex.InnerException?.Message ?? ex.Message}");
                }
                finally
                {
                    if (task.IsCompleted)
                        task.Dispose();
                    else
                        task.ContinueWith(t => t.Dispose(), TaskScheduler.Default);

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
                EventLogger.Error($"{name} stop error: {ex.Message}");
            }
            finally
            {
                work = null;
            }
        }

        //---------- Periodic Tasks ------------//

        /// <summary>
        /// Starts the periodic task that calculates current network speed.
        /// Runs every second and computes speed as delta from previous totals.
        /// </summary>
        private void StartSpeedMonitoring()
        {
            long tempDownload = 0;
            long tempUpload = 0;

            networkSpeedWork = new PeriodicWork("Network speed", TimeSpan.FromSeconds(1));
            Debug.WriteLine("Operation Started : Network capture");

            networkSpeedWork.Start(_ =>
            {
                // Read current totals (atomic reads)
                long currentDown = Interlocked.Read(ref CurrentSessionDownloadData);
                long currentUp = Interlocked.Read(ref CurrentSessionUploadData);

                // Calculate speed based on user's preferred format
                if (SettingsManager.Current.NetworkSpeedFormat == 0)
                {
                    // Bits per second
                    DownloadSpeed = (currentDown - tempDownload) * 8;
                    UploadSpeed = (currentUp - tempUpload) * 8;
                }
                else
                {
                    // Bytes per second
                    DownloadSpeed = currentDown - tempDownload;
                    UploadSpeed = currentUp - tempUpload;
                }

                // Store for next iteration's delta calculation
                tempDownload = currentDown;
                tempUpload = currentUp;

                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Starts the periodic task that pushes accumulated process data to the database.
        /// Runs every 5 seconds to batch writes and reduce I/O.
        /// </summary>
        private void StartDbPush()
        {
            dbPushWork = new PeriodicWork("DB push", TimeSpan.FromSeconds(5));
            Debug.WriteLine("Operation Started : DB push");

            dbPushWork.Start(_ =>
            {
#if DEBUG
                var sw = Stopwatch.StartNew();
#endif
                lock (PushToDBBuffer)
                {
                    if (PushToDBBuffer.Count > 0)
                    {
                        using var db = new ApplicationDB(AdapterName);
                        foreach (var (key, proc) in PushToDBBuffer)
                        {
                            if (proc != null)
                                db.PushToDB(key, proc.CurrentDataRecv, proc.CurrentDataSend);
                        }
                        PushToDBBuffer.Clear();
                    }
                }
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
            PacketTask = Task.Run(() =>
            {
                if (kernelSession != null) return;

                try
                {
                    // Create kernel trace session (requires admin)
                    kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
                    kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);

                    // Subscribe to all TCP/UDP send/receive events
                    // All events funnel through ProcessPacket for unified handling
                    var kernel = kernelSession.Source.Kernel;

                    // Receive events (download)
                    kernel.TcpIpRecv += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);
                    kernel.TcpIpRecvIPV6 += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);
                    kernel.UdpIpRecv += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);
                    kernel.UdpIpRecvIPV6 += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);

                    // Send events (upload)
                    kernel.TcpIpSend += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);
                    kernel.TcpIpSendIPV6 += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);
                    kernel.UdpIpSend += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);
                    kernel.UdpIpSendIPV6 += data => ProcessPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);

                    // Blocks until session is disposed
                    kernelSession.Source.Process();
                }
                catch (Exception ex)
                {
                    EventLogger.Error(ex.Message);
                }
            });
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

            // Cache address bytes to avoid repeated allocations
            var srcBytes = src.GetAddressBytes();
            var destBytes = dest.GetAddressBytes();

            // Check if packet involves our local IP
            bool isSrc = ByteArray.Compare(srcBytes, localIp);
            bool isDest = ByteArray.Compare(destBytes, localIp);

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
            // Unsubscribe from network change events
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;

            // Cancel any pending debounce
            lock (debounceLock)
            {
                networkChangeDebounce?.Cancel();
                networkChangeDebounce?.Dispose();
                networkChangeDebounce = null;
            }

            // Stop monitoring if active
            if (IsNetworkOnline != "Disconnected")
                EndNetworkProcess();

            // Flush any remaining buffered data to database
            lock (PushToDBBuffer)
            {
                if (PushToDBBuffer.Count > 0)
                {
                    using var db = new ApplicationDB(AdapterName);
                    foreach (var (key, proc) in PushToDBBuffer)
                    {
                        if (proc != null)
                            db.PushToDB(key, proc.CurrentDataRecv, proc.CurrentDataSend);
                    }
                    PushToDBBuffer.Clear();
                }
            }
        }
    }
}