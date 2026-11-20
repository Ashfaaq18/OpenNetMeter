using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;
using OpenNetMeter.Views.Controls.Charting;

namespace OpenNetMeter.Tests;

/// <summary>
/// While the main window is hidden or minimized (or another tab is showing) nothing displays the Summary page, so its
/// view model keeps every value current but raises no notifications for them, skips the weekly trend's periodic read,
/// and refreshes everything once when the page comes back. The mini widget keeps updating through the per-flush
/// <see cref="SummaryViewModel.Flushed"/> event whatever the page is doing.
/// </summary>
public class SummaryActivityTests
{
    private static readonly DateTime Today = DateTime.Today;

    private static readonly string[] PageNames =
    [
        nameof(SummaryViewModel.ProcessCount),
        nameof(SummaryViewModel.CurrentSessionDownloadText),
        nameof(SummaryViewModel.CurrentSessionUploadText),
        nameof(SummaryViewModel.TotalFromDateDownloadText),
        nameof(SummaryViewModel.TotalFromDateUploadText),
        nameof(SummaryViewModel.RecordingElapsedText),
        nameof(SummaryViewModel.RecordingDownloadText),
        nameof(SummaryViewModel.RecordingUploadText),
    ];

    private static readonly string[] SpeedNames =
    [
        nameof(SummaryViewModel.DownloadSpeedText),
        nameof(SummaryViewModel.UploadSpeedText),
    ];

    private static readonly string[] RowNames =
    [
        nameof(SummaryProcessRowViewModel.CurrentDownloadBytes),
        nameof(SummaryProcessRowViewModel.CurrentUploadBytes),
        nameof(SummaryProcessRowViewModel.TotalDownloadBytes),
        nameof(SummaryProcessRowViewModel.TotalUploadBytes),
        nameof(SummaryProcessRowViewModel.CurrentDownload),
        nameof(SummaryProcessRowViewModel.CurrentUpload),
        nameof(SummaryProcessRowViewModel.TotalDownload),
        nameof(SummaryProcessRowViewModel.TotalUpload),
    ];

    private sealed class NoLinks : IExternalLinkService
    {
        public void Open(string uri) { }
    }

    private sealed class FakeImage : Avalonia.Media.IImage
    {
        public Avalonia.Size Size => new(16, 16);
        public void Draw(Avalonia.Media.DrawingContext context, Avalonia.Rect sourceRect, Avalonia.Rect destRect) { }
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

        public Harness(
            Func<string, DateTime, DateTime, (long download, long upload)>? readTotals = null,
            Func<string, DateTime, DateTime, Dictionary<int, (long download, long upload)>>? readDailyTotals = null)
        {
            Summary = new SummaryViewModel(
                Capture,
                new PlaceholderProcessIconService(),
                new NoLinks(),
                new PlaceholderFileSaveDialogService(),
                new PlaceholderFileRevealService(),
                Post,
                () => 1_000_000,
                startFallbackTimer: false,
                readTotals ?? ((_, _, _) => (0, 0)),
                readDailyTotals ?? ((_, _, _) => []));
            Summary.PropertyChanged += (_, e) => Raised.Add(e.PropertyName);
            Summary.Flushed += (_, _) => Flushes++;
        }

        public FakeCapture Capture { get; } = new();
        public SummaryViewModel Summary { get; }
        public List<string?> Raised { get; } = [];
        public int Flushes { get; private set; }

        private void Post(Action action)
        {
            lock (posted)
                posted.Add(action);
        }

        public void RunPosted()
        {
            Action[] toRun;
            lock (posted)
            {
                toRun = [.. posted];
                posted.Clear();
            }

            foreach (var action in toRun)
                action();
        }

        public void Flush(params ProcessTraffic[] traffic)
        {
            Capture.RaiseBatch(traffic);
            RunPosted();
        }
    }

    private static string Bytes(long bytes) => ByteSizeFormatter.FormatBytes(bytes);

    private static Dictionary<int, (long download, long upload)> Daily(DateTime day, long download, long upload) =>
        new() { [UsageDatabase.ToDateInt(day)] = (download, upload) };

    private static List<string?> Watch(SummaryProcessRowViewModel row)
    {
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    [Fact]
    public void Inactive_RaisesNoPageOrRowNotifications_ButEveryValueKeepsAdvancing()
    {
        var h = new Harness();
        h.Flush(new ProcessTraffic("chrome", 100, 10));
        var row = Assert.Single(h.Summary.ActiveProcesses);
        var rowRaised = Watch(row);

        h.Summary.SetUiActive(false);
        h.Summary.SetSpeedActive(false);
        h.Raised.Clear();

        h.Flush(new ProcessTraffic("chrome", 50, 5), new ProcessTraffic("teams", 7, 0));
        h.Flush(new ProcessTraffic("chrome", 25, 0));

        // Nothing was announced: no card, speed or row property, whether or not it changed.
        Assert.Empty(h.Raised);
        Assert.Empty(rowRaised);

        // Yet every value is current.
        Assert.Equal(3, h.Summary.SpeedHistory.Count);
        Assert.Equal(new SpeedSample(25, 0), h.Summary.SpeedHistory[2]);
        Assert.Equal(Bytes(100 + 50 + 7 + 25), h.Summary.CurrentSessionDownloadText);
        Assert.Equal(Bytes(10 + 5), h.Summary.CurrentSessionUploadText);
        Assert.Equal(SummaryViewModel.FormatSpeed(25) + "ps", h.Summary.DownloadSpeedText);
        Assert.Equal(2, h.Summary.ProcessCount);
        Assert.Equal(175, row.TotalDownloadBytes);
        Assert.Equal(25, row.CurrentDownloadBytes);
        Assert.Equal(15, row.TotalUploadBytes);
        Assert.Equal(0, row.CurrentUploadBytes);
        var teams = Assert.Single(h.Summary.ActiveProcesses, p => p.ProcessName == "teams");
        Assert.Equal(7, teams.TotalDownloadBytes);
        Assert.Equal(0, teams.CurrentDownloadBytes);
    }

    [Fact]
    public void BecomingActive_RaisesOneRefreshOfThePageAndOfEachRow()
    {
        var h = new Harness();
        h.Summary.SetUiActive(false);
        h.Flush(new ProcessTraffic("chrome", 100, 10), new ProcessTraffic("teams", 5, 0));
        h.Flush(new ProcessTraffic("chrome", 40, 0));
        var rows = h.Summary.ActiveProcesses.ToList();
        var rowRaised = rows.ToDictionary(r => r.ProcessName, Watch);
        h.Raised.Clear();

        h.Summary.SetUiActive(true);

        // One of each page property, and once per row every value property: one refresh, not one per missed flush.
        Assert.Equal(PageNames.OrderBy(n => n), h.Raised.Cast<string>().OrderBy(n => n));
        foreach (var row in rows)
            Assert.Equal(RowNames.OrderBy(n => n), rowRaised[row.ProcessName].Cast<string>().OrderBy(n => n));

        // From here on flushes notify as usual.
        h.Raised.Clear();
        h.Flush(new ProcessTraffic("chrome", 1, 0));
        Assert.Contains(nameof(SummaryViewModel.CurrentSessionDownloadText), h.Raised);
        Assert.Contains(nameof(SummaryViewModel.DownloadSpeedText), h.Raised);
        Assert.Contains(nameof(SummaryProcessRowViewModel.TotalDownload), rowRaised["chrome"]);
    }

    [Fact]
    public void SwitchingToTheSameStateRaisesNothing()
    {
        var h = new Harness();
        h.Flush(new ProcessTraffic("chrome", 100, 10));
        h.Raised.Clear();

        h.Summary.SetUiActive(true);
        h.Summary.SetSpeedActive(true);
        Assert.Empty(h.Raised);

        h.Summary.SetUiActive(false);
        h.Summary.SetUiActive(false);
        h.Summary.SetSpeedActive(false);
        h.Summary.SetSpeedActive(false);
        h.Raised.Clear();

        h.Summary.SetUiActive(true);
        h.Summary.SetUiActive(true);
        Assert.Equal(PageNames.Length, h.Raised.Count);
    }

    [Fact]
    public void Flushed_IsRaisedExactlyOncePerFlush_WhetherOrNotThePageIsActive()
    {
        var h = new Harness();

        h.Flush(new ProcessTraffic("chrome", 1, 0));
        h.Flush();
        Assert.Equal(2, h.Flushes);

        h.Summary.SetUiActive(false);
        h.Summary.SetSpeedActive(false);
        h.Flush(new ProcessTraffic("chrome", 1, 0));
        h.Flush();
        h.Flush();
        Assert.Equal(5, h.Flushes);
    }

    [Fact]
    public void Flushed_IsRaisedByTheFallbackTickToo()
    {
        long now = 1_000_000;
        var capture = new FakeCapture();
        var summary = new SummaryViewModel(
            capture,
            new PlaceholderProcessIconService(),
            new NoLinks(),
            new PlaceholderFileSaveDialogService(),
            new PlaceholderFileRevealService(),
            _ => { },
            () => now,
            startFallbackTimer: false);
        var flushes = 0;
        summary.Flushed += (_, _) => flushes++;
        summary.SetUiActive(false);

        now += SummaryViewModel.FallbackIdleMilliseconds;
        summary.RunFallbackTick();

        Assert.Equal(1, flushes);
    }

    [Fact]
    public void TheStatusBarSpeedTexts_FollowTheirOwnSwitch_NotThePageSwitch()
    {
        var h = new Harness();

        // The Summary tab isn't showing but the window is: the status bar still needs the speeds.
        h.Summary.SetUiActive(false);
        h.Flush(new ProcessTraffic("chrome", 100, 0));
        Assert.Equal(SpeedNames.OrderBy(n => n), h.Raised.Cast<string>().OrderBy(n => n));

        h.Raised.Clear();
        h.Summary.SetSpeedActive(false);
        h.Flush(new ProcessTraffic("chrome", 100, 0));
        Assert.Empty(h.Raised);

        h.Summary.SetSpeedActive(true);
        Assert.Equal(SpeedNames.OrderBy(n => n), h.Raised.Cast<string>().OrderBy(n => n));
    }

    [Fact]
    public void ARowCreatedWhileInactive_IsQuiet_AndRefreshedWhenThePageComesBack()
    {
        var h = new Harness();
        h.Summary.SetUiActive(false);

        h.Flush(new ProcessTraffic("chrome", 100, 0));
        h.Flush(new ProcessTraffic("chrome", 100, 0));
        var row = Assert.Single(h.Summary.ActiveProcesses);
        var rowRaised = Watch(row);
        h.Flush(new ProcessTraffic("chrome", 100, 0));
        Assert.Empty(rowRaised);
        Assert.DoesNotContain(nameof(SummaryViewModel.ProcessCount), h.Raised);
        Assert.Equal(300, row.TotalDownloadBytes);

        h.Summary.SetUiActive(true);

        Assert.Equal(RowNames.Length, rowRaised.Count);
        Assert.Contains(nameof(SummaryViewModel.ProcessCount), h.Raised);
    }

    [Fact]
    public void TheRowsOtherUpdatePaths_AreQuietWhileInactiveToo()
    {
        var row = new SummaryProcessRowViewModel("chrome", new NoLinks());
        var raised = Watch(row);
        row.SetUiActive(false);

        row.ApplyTraffic(10, isReceive: true);
        row.ApplyTraffic(4, isReceive: false);
        row.ResetCurrent();

        Assert.Empty(raised);
        Assert.Equal(10, row.TotalDownloadBytes);
        Assert.Equal(4, row.TotalUploadBytes);
        Assert.Equal(0, row.CurrentDownloadBytes);

        row.SetUiActive(true);
        Assert.Equal(RowNames.Length, raised.Count);
    }

    [Fact]
    public void ARowStillAnnouncesItsIconWhileInactive()
    {
        var row = new SummaryProcessRowViewModel("chrome", new NoLinks());
        var raised = Watch(row);
        row.SetUiActive(false);

        row.Icon = new FakeImage();

        Assert.Equal([nameof(SummaryProcessRowViewModel.Icon)], raised);
    }

    [Fact]
    public void ARecordingKeepsCountingWhileInactive_AndItsHeaderRefreshesOnReturn()
    {
        var h = new Harness();
        h.Summary.StartRecordingCommand.Execute(null);
        h.Summary.SetUiActive(false);
        h.Raised.Clear();

        h.Flush(new ProcessTraffic("chrome", 300, 20));
        h.Flush(new ProcessTraffic("chrome", 200, 0));

        Assert.DoesNotContain(nameof(SummaryViewModel.RecordingDownloadText), h.Raised);
        Assert.DoesNotContain(nameof(SummaryViewModel.RecordingElapsedText), h.Raised);
        Assert.Equal(Bytes(500), h.Summary.RecordingDownloadText);
        Assert.Equal(Bytes(20), h.Summary.RecordingUploadText);

        h.Raised.Clear();
        h.Summary.SetUiActive(true);
        Assert.Contains(nameof(SummaryViewModel.RecordingElapsedText), h.Raised);
        Assert.Contains(nameof(SummaryViewModel.RecordingDownloadText), h.Raised);
        Assert.Contains(nameof(SummaryViewModel.RecordingUploadText), h.Raised);
    }

    [Fact]
    public void StartingAndStoppingARecording_StillNotifiesWhileInactive()
    {
        var h = new Harness();
        h.Summary.SetUiActive(false);
        h.Raised.Clear();

        h.Summary.StartRecordingCommand.Execute(null);

        // A user action, not a tick: the header's visibility bindings must follow it.
        Assert.Contains(nameof(SummaryViewModel.IsRecording), h.Raised);
    }

    [Fact]
    public async Task TheWeeklyTrendReadIsSkippedWhileInactive_AndRunsOnceOnReturn()
    {
        var reads = 0;
        var days = Daily(Today, 500, 50);
        var h = new Harness(null, (_, _, _) =>
        {
            Interlocked.Increment(ref reads);
            return days;
        });
        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();
        Assert.Equal(1, reads);
        Assert.Equal(Bytes(550), h.Summary.WeeklyTrend.TotalText);

        h.Summary.SetUiActive(false);
        days = Daily(Today, 900, 100);
        for (var tick = 0; tick < 40; tick++)
            h.Flush();
        await h.Summary.PendingUsageReads;
        Assert.Equal(1, reads);
        Assert.Equal(Bytes(550), h.Summary.WeeklyTrend.TotalText);

        // Back on screen with a stale trend: it is re-read right away, once.
        h.Summary.SetUiActive(true);
        await h.Summary.PendingUsageReads;
        Assert.Equal(2, reads);
        h.RunPosted();
        Assert.Equal(Bytes(1_000), h.Summary.WeeklyTrend.TotalText);

        h.Summary.SetUiActive(false);
        h.Summary.SetUiActive(true);
        await h.Summary.PendingUsageReads;
        Assert.Equal(2, reads);
    }

    [Fact]
    public async Task ComingBackToAFreshWeeklyTrend_DoesNotReReadIt_AndTheCadenceContinues()
    {
        var reads = 0;
        var h = new Harness(null, (_, _, _) =>
        {
            Interlocked.Increment(ref reads);
            return Daily(Today, 500, 50);
        });
        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();
        Assert.Equal(1, reads);

        // Away for only a few ticks: the trend is not stale yet.
        h.Summary.SetUiActive(false);
        for (var tick = 0; tick < 5; tick++)
            h.Flush();
        h.Summary.SetUiActive(true);
        await h.Summary.PendingUsageReads;
        Assert.Equal(1, reads);

        // The countdown carried on through the absence: 5 ticks are counted, so 10 more make fifteen.
        for (var tick = 0; tick < 9; tick++)
            h.Flush();
        await h.Summary.PendingUsageReads;
        Assert.Equal(1, reads);
        h.Flush();
        await h.Summary.PendingUsageReads;
        Assert.Equal(2, reads);
    }

    [Fact]
    public void ClearOnDisconnect_WhileInactive_NotifiesNoPageValues_ButTellsTheWidget()
    {
        var h = new Harness();
        h.Flush(new ProcessTraffic("chrome", 100, 0));
        h.Summary.SetUiActive(false);
        h.Summary.SetSpeedActive(false);
        var widgetChanges = 0;
        h.Summary.WidgetStateChanged += (_, _) => widgetChanges++;
        h.Raised.Clear();

        h.Summary.ClearOnDisconnect();

        Assert.Empty(h.Summary.ActiveProcesses);
        Assert.DoesNotContain(nameof(SummaryViewModel.CurrentSessionDownloadText), h.Raised);
        Assert.DoesNotContain(nameof(SummaryViewModel.ProcessCount), h.Raised);
        Assert.DoesNotContain(nameof(SummaryViewModel.DownloadSpeedText), h.Raised);
        Assert.Equal(1, widgetChanges);
    }

    [Fact]
    public async Task WidgetStateChanged_IsRaisedWhenTheWidgetTotalIsRebased_EvenWhileInactive()
    {
        var h = new Harness((_, _, _) => (1_000, 200), (_, _, _) => []);
        h.Summary.SetUiActive(false);
        var widgetChanges = 0;
        h.Summary.WidgetStateChanged += (_, _) => widgetChanges++;

        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();

        Assert.Equal(1, widgetChanges);
        Assert.Equal(Bytes(1_000), h.Summary.WidgetUsageDownloadText);
        // The Usage From card's own notifications are held back with the rest of the page.
        Assert.DoesNotContain(nameof(SummaryViewModel.TotalFromDateDownloadText), h.Raised);
        Assert.Equal(Bytes(1_000), h.Summary.TotalFromDateDownloadText);
    }

    [Fact]
    public void RefreshSpeedDisplayFormat_TellsTheWidget_AndTheStatusBarOnlyWhileItIsActive()
    {
        var h = new Harness();
        var widgetChanges = 0;
        h.Summary.WidgetStateChanged += (_, _) => widgetChanges++;

        h.Summary.RefreshSpeedDisplayFormat();
        Assert.Equal(1, widgetChanges);
        Assert.Equal(SpeedNames.OrderBy(n => n), h.Raised.Cast<string>().OrderBy(n => n));

        h.Summary.SetSpeedActive(false);
        h.Raised.Clear();
        h.Summary.RefreshSpeedDisplayFormat();
        Assert.Equal(2, widgetChanges);
        Assert.Empty(h.Raised);
    }
}
