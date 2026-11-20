using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

/// <summary>
/// The mini widget is on screen whenever the app runs, so it follows the Summary's per-flush event rather than the
/// property changes the Summary holds back while the main window is hidden: one sync per flush, however many
/// properties changed and whatever the window is doing.
/// </summary>
public class MiniWidgetSyncTests
{
    private sealed class NoLinks : IExternalLinkService
    {
        public void Open(string uri) { }
    }

    private sealed class FakeCapture : INetworkCaptureService
    {
        public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<NetworkTrafficBatchEventArgs>? TrafficBatchObserved;

        public NetworkSnapshotChangedEventArgs? CurrentSnapshot => null;

        public void Start() { }
        public void Stop() { }
        public IReadOnlyList<NetworkAdapterOption> GetAvailableAdapters() => [];
        public void RefreshAdapterSelection() { }
        public void Dispose() { }

        public void RaiseBatch(params ProcessTraffic[] traffic) =>
            TrafficBatchObserved?.Invoke(this, new NetworkTrafficBatchEventArgs(traffic));
    }

    private sealed class Harness
    {
        private readonly List<Action> posted = [];

        public Harness(bool usageFromTotal = false)
        {
            UsageFromTotal = usageFromTotal;
            Summary = new SummaryViewModel(
                Capture,
                new PlaceholderProcessIconService(),
                new NoLinks(),
                new PlaceholderFileSaveDialogService(),
                new PlaceholderFileRevealService(),
                posted.Add,
                () => 1_000_000,
                startFallbackTimer: false,
                (_, _, _) => (1_000, 200),
                (_, _, _) => []);
            Sync = new MiniWidgetSync(Summary, () => UsageFromTotal, Synced.Add);
        }

        public FakeCapture Capture { get; } = new();
        public SummaryViewModel Summary { get; }
        public MiniWidgetSync Sync { get; }
        public bool UsageFromTotal { get; set; }
        public List<MiniWidgetTexts> Synced { get; } = [];

        public void Flush(params ProcessTraffic[] traffic)
        {
            Capture.RaiseBatch(traffic);
            var toRun = posted.ToArray();
            posted.Clear();
            foreach (var action in toRun)
                action();
        }
    }

    private static string Bytes(long bytes) => ByteSizeFormatter.FormatBytes(bytes);

    [Fact]
    public void SyncsOncePerFlush_NotOncePerChangedProperty()
    {
        var h = new Harness();

        h.Flush(new ProcessTraffic("chrome", 100, 10));
        Assert.Single(h.Synced);

        h.Flush(new ProcessTraffic("chrome", 200, 20));
        h.Flush();
        Assert.Equal(3, h.Synced.Count);
    }

    [Fact]
    public void KeepsSyncingEveryFlushWhileThePageAndStatusBarAreInactive()
    {
        var h = new Harness();
        h.Summary.SetUiActive(false);
        h.Summary.SetSpeedActive(false);
        var summaryNotifications = 0;
        h.Summary.PropertyChanged += (_, _) => summaryNotifications++;

        h.Flush(new ProcessTraffic("chrome", 100, 10));
        h.Flush(new ProcessTraffic("chrome", 50, 5));
        h.Flush();

        // The Summary said nothing, yet the widget was updated for every flush.
        Assert.Equal(0, summaryNotifications);
        Assert.Equal(3, h.Synced.Count);
        Assert.Equal(h.Summary.DownloadSpeedText, h.Synced[2].DownloadSpeedText);
        Assert.Equal(SummaryViewModel.FormatSpeed(0) + "ps", h.Synced[2].DownloadSpeedText);
        Assert.Equal(Bytes(150), h.Synced[2].TotalDownloadText);
        Assert.Equal(Bytes(15), h.Synced[2].TotalUploadText);
    }

    [Fact]
    public void SessionMode_ShowsTheSessionTotals()
    {
        var h = new Harness(usageFromTotal: false);

        h.Flush(new ProcessTraffic("chrome", 100, 10));

        var texts = Assert.Single(h.Synced);
        Assert.Equal("Total:", texts.TotalLabel);
        Assert.Equal("Current session", texts.TotalToolTip);
        Assert.Equal(Bytes(100), texts.TotalDownloadText);
        Assert.Equal(Bytes(10), texts.TotalUploadText);
        Assert.Equal(h.Summary.DownloadSpeedText, texts.DownloadSpeedText);
        Assert.Equal(h.Summary.UploadSpeedText, texts.UploadSpeedText);
    }

    [Fact]
    public void UsageFromMode_ShowsTheWidgetTotalAndWordsItByDate()
    {
        var h = new Harness(usageFromTotal: true);

        h.Flush(new ProcessTraffic("chrome", 100, 10));

        var texts = Assert.Single(h.Synced);
        Assert.Equal(h.Summary.WidgetUsageDownloadText, texts.TotalDownloadText);
        Assert.Equal(h.Summary.WidgetUsageUploadText, texts.TotalUploadText);
        Assert.Equal("Today:", texts.TotalLabel);
        Assert.Equal("Usage today, since midnight", texts.TotalToolTip);
        Assert.Equal("Usage from today", texts.UsageFromMenuHeader);

        h.Summary.SetWidgetUsageFrom(DateTime.Today.AddDays(-3), followToday: false);
        var last = h.Synced[^1];
        Assert.Equal("Since:", last.TotalLabel);
        Assert.StartsWith("Usage since ", last.TotalToolTip);
        Assert.StartsWith("Usage from ", last.UsageFromMenuHeader);
        Assert.NotEqual("Usage from today", last.UsageFromMenuHeader);
    }

    [Fact]
    public async Task ARebasedWidgetTotal_SyncsOnceWithoutWaitingForTheNextFlush()
    {
        var h = new Harness(usageFromTotal: true);

        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        Assert.Empty(h.Synced);

        // The read's result is applied by the post to the UI thread.
        h.Flush();
        // (the flush above ran the posted result and then its own flush)
        Assert.Equal(2, h.Synced.Count);
        Assert.Equal(Bytes(1_000), h.Synced[0].TotalDownloadText);
    }

    [Fact]
    public void ClearOnDisconnect_ResetsTheWidget()
    {
        var h = new Harness();
        h.Flush(new ProcessTraffic("chrome", 100, 10));
        h.Synced.Clear();

        h.Summary.ClearOnDisconnect();

        var texts = Assert.Single(h.Synced);
        Assert.Equal(Bytes(0), texts.TotalDownloadText);
    }

    [Fact]
    public void Dispose_StopsSyncing()
    {
        var h = new Harness();
        h.Flush(new ProcessTraffic("chrome", 100, 10));
        Assert.Single(h.Synced);

        h.Sync.Dispose();
        h.Flush(new ProcessTraffic("chrome", 100, 10));
        h.Summary.ClearOnDisconnect();

        Assert.Single(h.Synced);
    }

    [Fact]
    public void SummaryPropertyChanges_DoNotSyncTheWidget_OnTheirOwn()
    {
        var h = new Harness();
        h.Summary.SetUiActive(false);
        h.Summary.SetUiActive(true);
        h.Summary.SetSpeedActive(false);
        h.Summary.SetSpeedActive(true);

        // Reactivating raises property changes (which the page needs) but they are not a reason to re-sync.
        Assert.Empty(h.Synced);
    }
}
