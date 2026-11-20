using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Avalonia.Media;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;
using OpenNetMeter.Views.Controls.Charting;

namespace OpenNetMeter.ViewModels;

public sealed class SummaryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly INetworkCaptureService networkCaptureService;
    private readonly IProcessIconService processIconService;
    private readonly IExternalLinkService externalLinkService;
    private readonly IFileSaveDialogService fileSaveDialogService;
    private readonly IFileRevealService fileRevealService;
    private readonly Dictionary<string, SummaryProcessRowViewModel> processIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object pendingLock = new();
    private readonly Dictionary<string, PendingTraffic> pendingByProcess = new(StringComparer.OrdinalIgnoreCase);
    // The capture tick is the app's one clock: every batch it raises drives one UI flush. This timer is only a
    // fallback for when no batch arrives (disconnected), so the chart still decays and the recording still ticks.
    private readonly DispatcherTimer? fallbackTimer;
    private readonly Action<Action> postToUi;
    private readonly Func<long> clockMilliseconds;
    private int flushQueued;
    private long lastBatchMilliseconds;
    private bool disposed;
    // While the main window is hidden or minimized, or another tab is showing, nothing displays the Summary page's
    // values. They are still kept up to date (counters, speed history, recording, row values) but their change
    // notifications are held back until the page is shown again (see SetUiActive). The status bar's speed texts
    // follow the window rather than the tab (see SetSpeedActive). Both default to on: a view model that nobody
    // has told otherwise notifies as it always did.
    private bool uiActive = true;
    private bool speedActive = true;

    /// <summary>
    /// How long without a batch before the fallback flushes. Longer than the capture tick's one second, so a
    /// late tick isn't answered with an extra (zero, then double) sample.
    /// </summary>
    internal const long FallbackIdleMilliseconds = 1500;
    private string? currentSortColumn;
    private bool sortDescending;
    private string activeAdapterName = string.Empty;
    // The adapter whose history the Usage From card, widget total and weekly trend show. Unlike
    // activeAdapterName it survives a disconnect, so those totals keep showing the last adapter's usage.
    private string usageAdapterName = string.Empty;
    private DateTime lastSeenDay = DateTime.Today;
    private long currentSessionDownload;
    private long currentSessionUpload;
    // Summary page "Usage From" card.
    private readonly UsageSinceTracker sinceDateUsage;
    // Mini widget "Usage from" total; deliberately independent of the Summary card's date.
    private readonly UsageSinceTracker widgetUsage;
    private bool widgetUsageFollowsToday = true;
    // The date the widget total was last asked to count from (the tracker's own date follows once the read lands).
    private DateTime widgetFromDate = DateTime.Today;
    private DateTimeOffset? sinceDate;
    // The database reads behind the Usage From card, the widget total and the weekly trend run on background tasks and
    // their results are applied on the UI thread (see StartUsageReads). Each card has a stamp that every request for it
    // bumps; a result is applied only if its stamp is still the latest, so an older read that finishes late is dropped.
    private int sinceReadStamp;
    private int widgetReadStamp;
    private int weeklyReadStamp;
    // A widget read was started and its result isn't applied yet. Keeps the midnight rollover from starting another
    // one on every tick (each would supersede the last, and none might ever land).
    private bool widgetReadPending;
    private Task pendingUsageReads = Task.CompletedTask;
    private readonly Func<string, DateTime, DateTime, Dictionary<int, (long download, long upload)>>? readDailyTotals;
    private long pendingDownloadBytes;
    private long pendingUploadBytes;
    private long latestDownloadBytesPerSecond;
    private long latestUploadBytesPerSecond;
    private int weeklyTrendTickCounter;
    private TrafficRecording? recording;
    private RecordingResultsViewModel? lastRecordingResults;
    private bool isRecordingResultsOpen;

    private readonly SpeedHistory speedHistory = new();

    public SummaryViewModel(
        INetworkCaptureService networkCaptureService,
        IProcessIconService processIconService,
        IExternalLinkService externalLinkService,
        IFileSaveDialogService fileSaveDialogService,
        IFileRevealService fileRevealService)
        : this(
            networkCaptureService,
            processIconService,
            externalLinkService,
            fileSaveDialogService,
            fileRevealService,
            action => Dispatcher.UIThread.Post(action, DispatcherPriority.Default),
            () => Environment.TickCount64,
            startFallbackTimer: true)
    {
    }

    /// <summary>
    /// The UI thread and the clock are injected so tests can run flushes and the fallback by hand, and the database
    /// readers so tests never touch the real database.
    /// </summary>
    internal SummaryViewModel(
        INetworkCaptureService networkCaptureService,
        IProcessIconService processIconService,
        IExternalLinkService externalLinkService,
        IFileSaveDialogService fileSaveDialogService,
        IFileRevealService fileRevealService,
        Action<Action> postToUi,
        Func<long> clockMilliseconds,
        bool startFallbackTimer,
        Func<string, DateTime, DateTime, (long download, long upload)>? readUsageTotals = null,
        Func<string, DateTime, DateTime, Dictionary<int, (long download, long upload)>>? readDailyTotals = null)
    {
        this.networkCaptureService = networkCaptureService;
        this.postToUi = postToUi;
        this.clockMilliseconds = clockMilliseconds;
        this.readDailyTotals = readDailyTotals;
        sinceDateUsage = readUsageTotals == null ? new UsageSinceTracker() : new UsageSinceTracker(readUsageTotals);
        widgetUsage = readUsageTotals == null ? new UsageSinceTracker() : new UsageSinceTracker(readUsageTotals);
        lastBatchMilliseconds = clockMilliseconds();
        this.processIconService = processIconService;
        this.externalLinkService = externalLinkService;
        this.fileSaveDialogService = fileSaveDialogService;
        this.fileRevealService = fileRevealService;

        ActiveProcesses = [];
        SortProcessesCommand = new ParameterRelayCommand(parameter =>
        {
            var column = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(column))
                return;

            SortProcesses(column);
        });

        StartRecordingCommand = new RelayCommand(StartRecording);
        StopRecordingCommand = new RelayCommand(StopRecording);
        ShowLastRecordingCommand = new RelayCommand(ShowLastRecording);
        CloseRecordingResultsCommand = new RelayCommand(CloseRecordingResults);

        WeeklyTrend = new WeeklyUsageTrendViewModel();

        sinceDate = DateTime.Today;
        this.networkCaptureService.TrafficBatchObserved += OnTrafficBatch;

        if (startFallbackTimer)
        {
            fallbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            fallbackTimer.Tick += (_, _) => RunFallbackTick();
            fallbackTimer.Start();
        }
    }
    public ObservableCollection<SummaryProcessRowViewModel> ActiveProcesses { get; }
    public ICommand SortProcessesCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand ShowLastRecordingCommand { get; }
    public ICommand CloseRecordingResultsCommand { get; }
    public WeeklyUsageTrendViewModel WeeklyTrend { get; }

    public SpeedHistory SpeedHistory => speedHistory;

    public string? CurrentSortColumn => currentSortColumn;
    public bool IsSortDescending => sortDescending;

    public string CurrentSessionDownloadText => ByteSizeFormatter.FormatBytes(currentSessionDownload);
    public string CurrentSessionUploadText => ByteSizeFormatter.FormatBytes(currentSessionUpload);
    public string TotalFromDateDownloadText => ByteSizeFormatter.FormatBytes(sinceDateUsage.Download);
    public string TotalFromDateUploadText => ByteSizeFormatter.FormatBytes(sinceDateUsage.Upload);
    public string WidgetUsageDownloadText => ByteSizeFormatter.FormatBytes(widgetUsage.Download);
    public string WidgetUsageUploadText => ByteSizeFormatter.FormatBytes(widgetUsage.Upload);
    public DateTime WidgetUsageFromDate => widgetUsage.FromDate;
    public string DownloadSpeedText => FormatSpeed(latestDownloadBytesPerSecond) + "ps";
    public string UploadSpeedText => FormatSpeed(latestUploadBytesPerSecond) + "ps";
    public int ProcessCount => ActiveProcesses.Count;
    public DateTime DateMin =>
        HistoryRetention.GetCutoff(DateMax, Properties.SettingsManager.Current.HistoryRetentionDays) ?? DateTime.MinValue;
    public DateTime DateMax => DateTime.Today;

    public bool IsRecording => recording is { IsRunning: true };
    public bool IsNotRecording => !IsRecording;
    public bool HasLastRecording => lastRecordingResults != null;
    public string RecordingElapsedText => recording is { } running
        ? TrafficRecording.FormatElapsed(running.GetDuration(DateTime.Now))
        : TrafficRecording.FormatElapsed(TimeSpan.Zero);
    public string RecordingDownloadText => ByteSizeFormatter.FormatBytes(recording?.TotalDownload ?? 0);
    public string RecordingUploadText => ByteSizeFormatter.FormatBytes(recording?.TotalUpload ?? 0);
    public RecordingResultsViewModel? LastRecordingResults => lastRecordingResults;

    public bool IsRecordingResultsOpen
    {
        get => isRecordingResultsOpen;
        private set
        {
            if (isRecordingResultsOpen == value)
                return;
            isRecordingResultsOpen = value;
            OnPropertyChanged(nameof(IsRecordingResultsOpen));
        }
    }

    public static string FormatSpeed(long bytesPerSecond)
    {
        var useBytes = Properties.SettingsManager.Current.NetworkSpeedFormat != 0;
        var magnitude = NetworkSpeed.NormalizeMagnitude(Properties.SettingsManager.Current.NetworkSpeedMagnitude);
        var value = useBytes ? bytesPerSecond : bytesPerSecond * 8;
        var (adjustedSize, mag) = NetworkSpeed.GetAdjustedSize(value, magnitude);
        return decimal.Round(adjustedSize, 2).ToString() + (useBytes ? NetworkSpeed.BytesSuffix(mag) : NetworkSpeed.BitsSuffix(mag));
    }
    
    public DateTimeOffset? SinceDate
    {
        get => sinceDate;
        set
        {
            var normalized = NormalizeSinceDate(value);
            if (sinceDate == normalized)
                return;

            sinceDate = normalized;
            StartUsageReads(UsageReads.SinceDateCard);
            OnPropertyChanged(nameof(SinceDate));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised once at the end of every flush (once per capture tick, or per fallback tick while no batch arrives),
    /// whether or not the page is showing. The mini widget follows this instead of the individual property changes,
    /// which are held back while the window is hidden.
    /// </summary>
    public event EventHandler? Flushed;

    /// <summary>
    /// Raised when something the mini widget shows changed outside a flush: a database read rebased the widget's
    /// total, the session counters restarted, or the speed display format changed.
    /// </summary>
    public event EventHandler? WidgetStateChanged;

    /// <summary>
    /// Tells the view model whether the Summary page is on screen (main window visible, not minimized, Summary tab
    /// selected). While it isn't, the card, process-row and recording notifications are held back and the weekly
    /// trend's periodic database refresh is skipped; going back to active raises one refresh of everything that
    /// changed meanwhile and re-reads the weekly trend if it went stale.
    /// </summary>
    public void SetUiActive(bool active)
    {
        if (uiActive == active)
            return;

        uiActive = active;
        foreach (var row in ActiveProcesses)
            row.SetUiActive(active);

        if (!active)
            return;

        NotifyPageValuesChanged();
        if (weeklyTrendTickCounter >= WeeklyTrendRefreshTicks)
            StartUsageReads(UsageReads.WeeklyTrend);
    }

    /// <summary>
    /// Tells the view model whether the status bar's speed texts are on screen (main window visible, not minimized,
    /// on any tab). Going back to active raises one refresh of them.
    /// </summary>
    public void SetSpeedActive(bool active)
    {
        if (speedActive == active)
            return;

        speedActive = active;
        if (active)
            NotifySpeedTextsChanged();
    }

    public void Dispose()
    {
        disposed = true;
        networkCaptureService.TrafficBatchObserved -= OnTrafficBatch;
        fallbackTimer?.Stop();
    }

    public void ClearOnDisconnect()
    {
        lock (pendingLock)
        {
            pendingDownloadBytes = 0;
            pendingUploadBytes = 0;
            pendingByProcess.Clear();
        }

        latestDownloadBytesPerSecond = 0;
        latestUploadBytesPerSecond = 0;
        currentSessionDownload = 0;
        currentSessionUpload = 0;
        // The DB-backed totals (Usage From card, widget total, weekly trend) keep showing the last
        // adapter's usage while disconnected; only the session counters restart.
        sinceDateUsage.FreezeSession();
        widgetUsage.FreezeSession();
        activeAdapterName = string.Empty;

        // A read that began before this was paired with session counters that have just restarted, so its result
        // would rebase the totals against a session that no longer exists. (The weekly trend doesn't depend on the
        // session, so an in-flight read of it is still good.)
        sinceReadStamp++;
        widgetReadStamp++;
        widgetReadPending = false;

        speedHistory.Clear();
        weeklyTrendTickCounter = 0;

        ActiveProcesses.Clear();
        processIndex.Clear();
        if (uiActive)
            NotifyPageValuesChanged();
        if (speedActive)
            NotifySpeedTextsChanged();
        NotifyWidgetUsageChanged();
    }

    /// <summary>
    /// Re-reads DB-backed totals after history was purged or deleted, clamping the "since" date to the new retention.
    /// The widget's date is clamped by <see cref="SettingsViewModel"/>, which then calls <see cref="SetWidgetUsageFrom"/>.
    /// </summary>
    public void RefreshAfterHistoryPurge()
    {
        var normalized = NormalizeSinceDate(sinceDate);
        if (sinceDate != normalized)
        {
            sinceDate = normalized;
            OnPropertyChanged(nameof(SinceDate));
        }

        StartUsageReads(UsageReads.All);
    }

    /// <summary>
    /// Sets the date the mini widget's "usage from" total counts from. When <paramref name="followToday"/>
    /// is set, the date moves to the new day at midnight.
    /// </summary>
    public void SetWidgetUsageFrom(DateTime fromDate, bool followToday)
    {
        widgetUsageFollowsToday = followToday;
        widgetFromDate = fromDate.Date;
        StartUsageReads(UsageReads.Widget);
    }

    public void SetActiveAdapter(string adapterName)
    {
        var normalized = adapterName?.Trim() ?? string.Empty;
        if (string.Equals(activeAdapterName, normalized, StringComparison.Ordinal))
            return;

        activeAdapterName = normalized;
        usageAdapterName = normalized;
        if (recording is { IsRunning: true })
            recording.NoteAdapter(activeAdapterName);

        StartUsageReads(UsageReads.All);
    }

    private void StartRecording()
    {
        if (IsRecording)
            return;

        // A recording keeps running across disconnects and adapter switches, so it is deliberately
        // not touched by ClearOnDisconnect; it only ends when the user presses Stop.
        recording = new TrafficRecording(DateTime.Now);
        recording.NoteAdapter(activeAdapterName);
        NotifyRecordingChanged();
    }

    private void StopRecording()
    {
        if (recording is not { IsRunning: true } stopping)
            return;

        stopping.Stop(DateTime.Now);
        lastRecordingResults = new RecordingResultsViewModel(stopping, processIconService, externalLinkService, fileSaveDialogService, fileRevealService);
        recording = null;
        IsRecordingResultsOpen = true;
        NotifyRecordingChanged();
    }

    private void ShowLastRecording()
    {
        if (HasLastRecording)
            IsRecordingResultsOpen = true;
    }

    private void CloseRecordingResults()
    {
        IsRecordingResultsOpen = false;
    }

    private void NotifyRecordingChanged()
    {
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsNotRecording));
        OnPropertyChanged(nameof(HasLastRecording));
        OnPropertyChanged(nameof(LastRecordingResults));
        OnPropertyChanged(nameof(RecordingElapsedText));
        OnPropertyChanged(nameof(RecordingDownloadText));
        OnPropertyChanged(nameof(RecordingUploadText));
    }

    /// <summary>
    /// A capture tick's traffic. Merged into the pending totals here (on the capture thread), then ONE UI flush is
    /// posted for it; a batch that arrives while a flush is still queued is simply drained by that flush.
    /// </summary>
    private void OnTrafficBatch(object? sender, NetworkTrafficBatchEventArgs e)
    {
        if (disposed)
            return;

        lock (pendingLock)
        {
            foreach (var traffic in e.Traffic)
            {
                if (traffic.Received <= 0 && traffic.Sent <= 0)
                    continue;

                if (!pendingByProcess.TryGetValue(traffic.Name, out var pending))
                {
                    pending = new PendingTraffic();
                    pendingByProcess[traffic.Name] = pending;
                }

                pending.DownloadBytes += traffic.Received;
                pendingDownloadBytes += traffic.Received;
                pending.UploadBytes += traffic.Sent;
                pendingUploadBytes += traffic.Sent;
            }
        }

        Volatile.Write(ref lastBatchMilliseconds, clockMilliseconds());
        QueueFlush();
    }

    private void QueueFlush()
    {
        if (Interlocked.Exchange(ref flushQueued, 1) == 0)
            postToUi(RunQueuedFlush);
    }

    private void RunQueuedFlush()
    {
        // Cleared first: a batch that arrives while this flush runs queues the next one.
        Volatile.Write(ref flushQueued, 0);
        if (disposed)
            return;

        FlushPendingTraffic();
    }

    /// <summary>
    /// What the fallback timer does every second: flush only if the capture tick has gone quiet (not running, or
    /// the adapter is disconnected). While batches arrive they are the only thing that flushes.
    /// </summary>
    internal void RunFallbackTick()
    {
        if (disposed)
            return;

        if (clockMilliseconds() - Volatile.Read(ref lastBatchMilliseconds) >= FallbackIdleMilliseconds)
            FlushPendingTraffic();
    }

    private void FlushPendingTraffic()
    {
        Dictionary<string, PendingTraffic> pendingSnapshot;
        long secondDownloadBytes;
        long secondUploadBytes;

        lock (pendingLock)
        {
            secondDownloadBytes = pendingDownloadBytes;
            secondUploadBytes = pendingUploadBytes;
            pendingDownloadBytes = 0;
            pendingUploadBytes = 0;

            pendingSnapshot = new Dictionary<string, PendingTraffic>(pendingByProcess, StringComparer.OrdinalIgnoreCase);
            pendingByProcess.Clear();
        }

        // Date bounds follow the calendar, so tell listeners when the day rolls over (any widget mode).
        var today = DateTime.Today;
        if (today != lastSeenDay)
        {
            lastSeenDay = today;
            OnPropertyChanged(nameof(DateMax));
            OnPropertyChanged(nameof(DateMin));
        }

        latestDownloadBytesPerSecond = secondDownloadBytes;
        latestUploadBytesPerSecond = secondUploadBytes;
        currentSessionDownload += secondDownloadBytes;
        currentSessionUpload += secondUploadBytes;
        sinceDateUsage.Advance(currentSessionDownload, currentSessionUpload);

        // A widget following "today" starts over at midnight (the new baseline lands when its read finishes); a fixed
        // date keeps accumulating.
        if (widgetUsageFollowsToday && widgetUsage.FromDate != DateTime.Today && !widgetReadPending)
            StartUsageReads(UsageReads.Widget);
        widgetUsage.Advance(currentSessionDownload, currentSessionUpload);

        speedHistory.Append(latestDownloadBytesPerSecond, latestUploadBytesPerSecond);
        ApplyProcessTick(pendingSnapshot);

        if (recording is { IsRunning: true })
        {
            foreach (var kvp in pendingSnapshot)
                recording.Add(kvp.Key, kvp.Value.DownloadBytes, kvp.Value.UploadBytes);

            if (uiActive)
            {
                OnPropertyChanged(nameof(RecordingElapsedText));
                OnPropertyChanged(nameof(RecordingDownloadText));
                OnPropertyChanged(nameof(RecordingUploadText));
            }
        }

        // The capture service pushes to the DB every 5s, so re-reading the week this often
        // keeps today's bar close to live without querying on every tick. Only while the card is on screen: the
        // counter keeps running meanwhile, so a stale trend is re-read as soon as the page is shown again.
        weeklyTrendTickCounter = Math.Min(weeklyTrendTickCounter + 1, WeeklyTrendRefreshTicks);
        if (uiActive && weeklyTrendTickCounter >= WeeklyTrendRefreshTicks)
            StartUsageReads(UsageReads.WeeklyTrend);

        if (uiActive)
        {
            OnPropertyChanged(nameof(CurrentSessionDownloadText));
            OnPropertyChanged(nameof(CurrentSessionUploadText));
            OnPropertyChanged(nameof(TotalFromDateDownloadText));
            OnPropertyChanged(nameof(TotalFromDateUploadText));
        }

        if (speedActive)
            NotifySpeedTextsChanged();

        Flushed?.Invoke(this, EventArgs.Empty);
    }

    // Everything the Summary page binds that a flush changes (the row values refresh themselves, see SetUiActive).
    private void NotifyPageValuesChanged()
    {
        OnPropertyChanged(nameof(ProcessCount));
        OnPropertyChanged(nameof(CurrentSessionDownloadText));
        OnPropertyChanged(nameof(CurrentSessionUploadText));
        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));
        OnPropertyChanged(nameof(RecordingElapsedText));
        OnPropertyChanged(nameof(RecordingDownloadText));
        OnPropertyChanged(nameof(RecordingUploadText));
    }

    private void NotifySpeedTextsChanged()
    {
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
    }

    /// <summary>Flush ticks (one per second) between reloads of the weekly trend card.</summary>
    private const int WeeklyTrendRefreshTicks = 15;

    [Flags]
    private enum UsageReads
    {
        SinceDateCard = 1,
        Widget = 2,
        WeeklyTrend = 4,
        All = SinceDateCard | Widget | WeeklyTrend
    }

    /// <summary>
    /// Completes once the results of the latest <see cref="StartUsageReads"/> have been handed to the UI thread
    /// (immediately when there was nothing to read). Lets tests wait for the background part.
    /// </summary>
    internal Task PendingUsageReads => pendingUsageReads;

    /// <summary>
    /// Starts the database reads behind the chosen cards on background tasks, all at once (each opens its own read-only
    /// connection), and applies every result in ONE UI-thread post when the last one is in. A rebase pairs the persisted
    /// totals with the session counters of the same moment, so the counters are captured here, as the reads start, and
    /// the result is rebased against those; whatever the session counted while the read ran is then added by Advance,
    /// as on every tick. A result is dropped if a newer request for the same card started after it (see the stamps), or
    /// if the session restarted meanwhile (ClearOnDisconnect). With no adapter there is nothing to read and the (empty)
    /// results are applied right here, as they were before the reads moved off the UI thread.
    /// </summary>
    private void StartUsageReads(UsageReads which)
    {
        var adapter = usageAdapterName;
        var sessionDownload = currentSessionDownload;
        var sessionUpload = currentSessionUpload;
        var inline = string.IsNullOrWhiteSpace(adapter);
        var reads = new List<Task<Action>>(3);

        if (which.HasFlag(UsageReads.SinceDateCard))
        {
            var stamp = ++sinceReadStamp;
            var fromDate = (sinceDate ?? DateTimeOffset.Now.Date).Date;
            reads.Add(RunRead(inline, () =>
            {
                var totals = sinceDateUsage.ReadTotals(adapter, fromDate);
                return () => ApplySinceDateBaseline(stamp, fromDate, sessionDownload, sessionUpload, totals);
            }));
        }

        if (which.HasFlag(UsageReads.Widget))
        {
            var stamp = ++widgetReadStamp;
            var fromDate = widgetUsageFollowsToday ? DateTime.Today : widgetFromDate;
            widgetReadPending = true;
            reads.Add(RunRead(inline, () =>
            {
                var totals = widgetUsage.ReadTotals(adapter, fromDate);
                return () => ApplyWidgetBaseline(stamp, fromDate, sessionDownload, sessionUpload, totals);
            }));
        }

        if (which.HasFlag(UsageReads.WeeklyTrend))
        {
            var stamp = ++weeklyReadStamp;
            weeklyTrendTickCounter = 0;
            reads.Add(RunRead(inline, () =>
            {
                var data = string.IsNullOrWhiteSpace(adapter) ? null
                    : readDailyTotals == null ? WeeklyUsageTrendViewModel.Read(adapter)
                    : WeeklyUsageTrendViewModel.Read(adapter, readDailyTotals);
                return () => ApplyWeeklyTrend(stamp, data);
            }));
        }

        if (inline)
        {
            foreach (var read in reads)
                read.Result();
            pendingUsageReads = Task.CompletedTask;
            return;
        }

        pendingUsageReads = PostUsageReadsAsync(reads);
    }

    // Runs the read (which returns the action that applies its result) on a background thread, or right here.
    private static Task<Action> RunRead(bool inline, Func<Action> read) =>
        inline ? Task.FromResult(read()) : Task.Run(read);

    private async Task PostUsageReadsAsync(List<Task<Action>> reads)
    {
        Action[] applies;
        try
        {
            applies = await Task.WhenAll(reads).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The readers log their own failures, so this isn't expected; keep the midnight rollover from waiting for it.
            EventLogger.Error("Failed to read usage totals", ex);
            postToUi(() => widgetReadPending = false);
            return;
        }

        postToUi(() =>
        {
            if (disposed)
                return;

            foreach (var apply in applies)
                apply();
        });
    }

    private void ApplySinceDateBaseline(int stamp, DateTime fromDate, long sessionDownload, long sessionUpload, (long download, long upload) totals)
    {
        if (stamp != sinceReadStamp)
            return;

        sinceDateUsage.Rebase(fromDate, sessionDownload, sessionUpload, totals);
        sinceDateUsage.Advance(currentSessionDownload, currentSessionUpload);

        if (uiActive)
        {
            OnPropertyChanged(nameof(TotalFromDateDownloadText));
            OnPropertyChanged(nameof(TotalFromDateUploadText));
        }
    }

    private void ApplyWidgetBaseline(int stamp, DateTime fromDate, long sessionDownload, long sessionUpload, (long download, long upload) totals)
    {
        if (stamp != widgetReadStamp)
            return;

        widgetReadPending = false;
        widgetUsage.Rebase(fromDate, sessionDownload, sessionUpload, totals);
        widgetUsage.Advance(currentSessionDownload, currentSessionUpload);
        NotifyWidgetUsageChanged();
    }

    private void ApplyWeeklyTrend(int stamp, WeeklyTrendData? data)
    {
        if (stamp != weeklyReadStamp)
            return;

        if (data == null)
            WeeklyTrend.Reset();
        else
            WeeklyTrend.Apply(data);
    }

    private void NotifyWidgetUsageChanged()
    {
        OnPropertyChanged(nameof(WidgetUsageFromDate));
        OnPropertyChanged(nameof(WidgetUsageDownloadText));
        OnPropertyChanged(nameof(WidgetUsageUploadText));
        WidgetStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private DateTimeOffset NormalizeSinceDate(DateTimeOffset? value)
    {
        var candidate = (value ?? DateTimeOffset.Now.Date).Date;
        if (candidate > DateMax)
            candidate = DateMax;
        if (candidate < DateMin)
            candidate = DateMin;
        return candidate;
    }

    private void ApplyProcessTick(Dictionary<string, PendingTraffic> pendingSnapshot)
    {
        var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in pendingSnapshot)
        {
            touched.Add(kvp.Key);

            if (!processIndex.TryGetValue(kvp.Key, out var row))
            {
                // Resolving a process icon can take a full process snapshot, so never do it on the flush tick:
                // the row starts with the cached/default icon and gets the real one when the background load ends.
                var newRow = new SummaryProcessRowViewModel(kvp.Key, externalLinkService, processIconService.GetCachedIcon(kvp.Key) as IImage);
                ProcessIconLoader.Attach(processIconService, kvp.Key, icon => newRow.Icon = icon);
                if (!uiActive)
                    newRow.SetUiActive(false);
                row = newRow;
                processIndex[kvp.Key] = row;
                ActiveProcesses.Add(row);
                if (uiActive)
                    OnPropertyChanged(nameof(ProcessCount));
            }

            row.ApplyTick(kvp.Value.DownloadBytes, kvp.Value.UploadBytes);
        }

        foreach (var kvp in processIndex)
        {
            if (!touched.Contains(kvp.Key))
                kvp.Value.ResetCurrent();
        }
    }

    private static readonly Dictionary<string, Func<SummaryProcessRowViewModel, IComparable>> SortSelectors = new(StringComparer.Ordinal)
    {
        ["Name"] = p => p.ProcessName,
        ["CurrentDownload"] = p => p.CurrentDownloadBytes,
        ["CurrentUpload"] = p => p.CurrentUploadBytes,
        ["TotalDownload"] = p => p.TotalDownloadBytes,
        ["TotalUpload"] = p => p.TotalUploadBytes
    };

    private void SortProcesses(string column)
    {
        sortDescending = string.Equals(currentSortColumn, column, StringComparison.Ordinal) ? !sortDescending : false;
        currentSortColumn = column;

        NotifySortPropertiesChanged();

        var selector = SortSelectors.GetValueOrDefault(column, SortSelectors["Name"]);
        var sorted = sortDescending
            ? ActiveProcesses.OrderByDescending(selector).ToList()
            : ActiveProcesses.OrderBy(selector).ToList();

        ActiveProcesses.Clear();
        foreach (var item in sorted)
            ActiveProcesses.Add(item);
    }

    private void NotifySortPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(IsSortDescending));
    }

    public void RefreshSpeedDisplayFormat()
    {
        speedHistory.NotifyDisplayFormatChanged();
        if (speedActive)
            NotifySpeedTextsChanged();
        WidgetStateChanged?.Invoke(this, EventArgs.Empty);
    }


    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class PendingTraffic
    {
        public long DownloadBytes;
        public long UploadBytes;
    }
}

public sealed class SummaryProcessRowViewModel : INotifyPropertyChanged
{
    private readonly IExternalLinkService externalLinkService;
    private IImage? icon;
    private long currentDownloadBytes;
    private long currentUploadBytes;
    private long totalDownloadBytes;
    private long totalUploadBytes;
    // False while the Summary page isn't on screen: values keep advancing but nothing is raised (see SetUiActive).
    private bool uiActive = true;

    public SummaryProcessRowViewModel(string processName, IExternalLinkService externalLinkService, IImage? icon = null)
    {
        ProcessName = processName;
        this.externalLinkService = externalLinkService;
        this.icon = icon;
        SearchProcessCommand = new RelayCommand(() =>
        {
            var encodedName = Uri.EscapeDataString(ProcessName);
            externalLinkService.Open($"https://www.google.com/search?q={encodedName}");
        });
    }

    public IImage? Icon
    {
        get => icon;
        set
        {
            if (ReferenceEquals(icon, value))
                return;
            icon = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

    public string ProcessName { get; }
    public ICommand SearchProcessCommand { get; }
    public long CurrentDownloadBytes => currentDownloadBytes;
    public long CurrentUploadBytes => currentUploadBytes;
    public long TotalDownloadBytes => totalDownloadBytes;
    public long TotalUploadBytes => totalUploadBytes;

    public string CurrentDownload => ByteSizeFormatter.FormatBytes(currentDownloadBytes);
    public string CurrentUpload => ByteSizeFormatter.FormatBytes(currentUploadBytes);
    public string TotalDownload => ByteSizeFormatter.FormatBytes(totalDownloadBytes);
    public string TotalUpload => ByteSizeFormatter.FormatBytes(totalUploadBytes);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Holds back the value change notifications while the page isn't on screen; the values themselves keep
    /// advancing. Going back to active raises one refresh of every value.
    /// </summary>
    public void SetUiActive(bool active)
    {
        if (uiActive == active)
            return;

        uiActive = active;
        if (active)
            NotifyValuesChanged();
    }

    public void ApplyTick(long secondDownloadBytes, long secondUploadBytes)
    {
        currentDownloadBytes = secondDownloadBytes;
        currentUploadBytes = secondUploadBytes;
        totalDownloadBytes += secondDownloadBytes;
        totalUploadBytes += secondUploadBytes;

        if (uiActive)
            NotifyValuesChanged();
    }

    public void ResetCurrent()
    {
        if (currentDownloadBytes == 0 && currentUploadBytes == 0)
            return;

        currentDownloadBytes = 0;
        currentUploadBytes = 0;
        if (!uiActive)
            return;

        OnPropertyChanged(nameof(CurrentDownloadBytes));
        OnPropertyChanged(nameof(CurrentUploadBytes));
        OnPropertyChanged(nameof(CurrentDownload));
        OnPropertyChanged(nameof(CurrentUpload));
    }

    private void NotifyValuesChanged()
    {
        OnPropertyChanged(nameof(CurrentDownloadBytes));
        OnPropertyChanged(nameof(CurrentUploadBytes));
        OnPropertyChanged(nameof(TotalDownloadBytes));
        OnPropertyChanged(nameof(TotalUploadBytes));
        OnPropertyChanged(nameof(CurrentDownload));
        OnPropertyChanged(nameof(CurrentUpload));
        OnPropertyChanged(nameof(TotalDownload));
        OnPropertyChanged(nameof(TotalUpload));
    }

    public void ApplyTraffic(long bytes, bool isReceive)
    {
        if (isReceive) // kept for compatibility with older call sites
        {
            currentDownloadBytes = bytes;
            totalDownloadBytes += bytes;
            if (!uiActive)
                return;

            OnPropertyChanged(nameof(CurrentDownloadBytes));
            OnPropertyChanged(nameof(TotalDownloadBytes));
            OnPropertyChanged(nameof(CurrentDownload));
            OnPropertyChanged(nameof(TotalDownload));
        }
        else
        {
            currentUploadBytes = bytes;
            totalUploadBytes += bytes;
            if (!uiActive)
                return;

            OnPropertyChanged(nameof(CurrentUploadBytes));
            OnPropertyChanged(nameof(TotalUploadBytes));
            OnPropertyChanged(nameof(CurrentUpload));
            OnPropertyChanged(nameof(TotalUpload));
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

