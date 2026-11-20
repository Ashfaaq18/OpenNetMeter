using System.Collections.Concurrent;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

/// <summary>
/// The Usage From card, the widget total and the weekly trend are read from the database on background tasks and
/// applied by one post to the UI thread. The readers are injected (nothing here touches a real database) and the
/// "UI thread" is a queue the test runs by hand, so the order in which results arrive is under the test's control.
/// </summary>
public class SummaryUsageReadTests
{
    private static readonly DateTime Today = DateTime.Today;

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
        }

        public FakeCapture Capture { get; } = new();
        public SummaryViewModel Summary { get; }

        public int PostedCount
        {
            get { lock (posted) return posted.Count; }
        }

        private void Post(Action action)
        {
            lock (posted)
                posted.Add(action);
        }

        /// <summary>Runs the posts queued for the "UI thread", oldest first.</summary>
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

    private static void WaitFor(ManualResetEventSlim gate) => gate.Wait(TimeSpan.FromSeconds(5));

    [Fact]
    public async Task SetActiveAdapter_ReadsOffTheCallingThread_AndAppliesAllCardsInOnePost()
    {
        var gate = new ManualResetEventSlim();
        var reads = 0;
        var h = new Harness(
            (_, _, _) =>
            {
                Interlocked.Increment(ref reads);
                WaitFor(gate);
                return (1_000, 200);
            },
            (_, _, _) =>
            {
                Interlocked.Increment(ref reads);
                WaitFor(gate);
                return Daily(Today, 500, 50);
            });
        var raised = new List<string?>();
        h.Summary.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Returns while every read is still blocked: nothing waits for the database.
        h.Summary.SetActiveAdapter("Wi-Fi");
        Assert.Equal(0, h.PostedCount);
        Assert.Equal(Bytes(0), h.Summary.TotalFromDateDownloadText);

        gate.Set();
        await h.Summary.PendingUsageReads;

        // Read, but not applied until the UI thread gets to it; and all of it arrives in a single post.
        Assert.Equal(1, h.PostedCount);
        Assert.Equal(Bytes(0), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(0), h.Summary.WidgetUsageDownloadText);
        Assert.Equal(3, reads);
        raised.Clear();

        h.RunPosted();

        Assert.Equal(Bytes(1_000), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(200), h.Summary.TotalFromDateUploadText);
        Assert.Equal(Bytes(1_000), h.Summary.WidgetUsageDownloadText);
        Assert.Equal(Bytes(200), h.Summary.WidgetUsageUploadText);
        Assert.Equal(Bytes(550), h.Summary.WeeklyTrend.TotalText);
        Assert.Contains(nameof(SummaryViewModel.TotalFromDateDownloadText), raised);
        Assert.Contains(nameof(SummaryViewModel.WidgetUsageDownloadText), raised);
    }

    [Fact]
    public async Task SetActiveAdapter_RunsTheThreeReadsAtTheSameTime()
    {
        // Each read waits until all three have started; run one after another they would never see that.
        using var allStarted = new CountdownEvent(3);
        var overlapped = new ConcurrentBag<bool>();
        bool Rendezvous()
        {
            allStarted.Signal();
            var ok = allStarted.Wait(TimeSpan.FromSeconds(5));
            overlapped.Add(ok);
            return ok;
        }

        var h = new Harness(
            (_, _, _) => { Rendezvous(); return (1, 1); },
            (_, _, _) => { Rendezvous(); return []; });

        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;

        Assert.Equal(3, overlapped.Count);
        Assert.All(overlapped, ok => Assert.True(ok));
    }

    [Fact]
    public async Task Result_IsRebasedAgainstTheSessionCountersFromWhenTheReadStarted()
    {
        var gate = new ManualResetEventSlim();
        var h = new Harness((_, _, _) => { WaitFor(gate); return (1_000, 0); });
        h.Flush(new ProcessTraffic("chrome", 100, 0));

        h.Summary.SetActiveAdapter("Wi-Fi");
        // Traffic that arrives while the read is running: the session counters move on to 130.
        h.Flush(new ProcessTraffic("chrome", 30, 0));
        Assert.Equal(Bytes(130), h.Summary.CurrentSessionDownloadText);

        gate.Set();
        await h.Summary.PendingUsageReads;
        h.RunPosted();

        // Persisted 1000 plus what the session counted since the read began (130 - 100), not since the session began.
        Assert.Equal(Bytes(1_030), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(1_030), h.Summary.WidgetUsageDownloadText);

        // And from there the total keeps following the session, as on every tick.
        h.Flush(new ProcessTraffic("chrome", 20, 0));
        Assert.Equal(Bytes(1_050), h.Summary.TotalFromDateDownloadText);
    }

    [Fact]
    public async Task ChangingAdapterBeforeTheResultArrives_DropsTheOlderResult()
    {
        var gateA = new ManualResetEventSlim();
        var h = new Harness((adapter, _, _) =>
        {
            if (adapter == "A")
            {
                WaitFor(gateA);
                return (111, 0);
            }

            return (222, 0);
        });

        h.Summary.SetActiveAdapter("A");
        var readsA = h.Summary.PendingUsageReads;
        h.Summary.SetActiveAdapter("B");
        await h.Summary.PendingUsageReads;
        h.RunPosted();
        Assert.Equal(Bytes(222), h.Summary.TotalFromDateDownloadText);

        // A's read finally finishes: it belongs to the previous adapter and must not replace B's totals.
        gateA.Set();
        await readsA;
        h.RunPosted();

        Assert.Equal(Bytes(222), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(222), h.Summary.WidgetUsageDownloadText);
    }

    [Fact]
    public async Task ChangingAdapterBeforeTheResultArrives_DropsTheOlderResultEvenIfItArrivesFirst()
    {
        var gateB = new ManualResetEventSlim();
        var h = new Harness((adapter, _, _) =>
        {
            if (adapter == "B")
            {
                WaitFor(gateB);
                return (222, 0);
            }

            return (111, 0);
        });

        h.Summary.SetActiveAdapter("A");
        var readsA = h.Summary.PendingUsageReads;
        h.Summary.SetActiveAdapter("B");
        var readsB = h.Summary.PendingUsageReads;

        await readsA;
        h.RunPosted();
        // Showing A's usage under B's name, even for a moment, would be wrong.
        Assert.Equal(Bytes(0), h.Summary.TotalFromDateDownloadText);

        gateB.Set();
        await readsB;
        h.RunPosted();

        Assert.Equal(Bytes(222), h.Summary.TotalFromDateDownloadText);
    }

    [Fact]
    public async Task SinceDate_ReadsInTheBackground_AndOnlyTheNewestChoiceIsApplied()
    {
        var h = new Harness((_, from, _) => (from == Today.AddDays(-2) ? 200 : from == Today.AddDays(-1) ? 100 : 5, 0));
        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();

        h.Summary.SinceDate = new DateTimeOffset(Today.AddDays(-2));
        var first = h.Summary.PendingUsageReads;
        h.Summary.SinceDate = new DateTimeOffset(Today.AddDays(-1));
        var second = h.Summary.PendingUsageReads;
        await Task.WhenAll(first, second);
        Assert.Equal(2, h.PostedCount);

        // The date picker changed twice; the older choice's total arriving afterwards must not win.
        h.RunPosted();

        Assert.Equal(Bytes(100), h.Summary.TotalFromDateDownloadText);
    }

    [Fact]
    public async Task SinceDate_ARunningReadDoesNotBlockTheSetter()
    {
        var gate = new ManualResetEventSlim();
        var h = new Harness((_, from, _) =>
        {
            if (from == Today.AddDays(-3))
                WaitFor(gate);
            return (300, 0);
        });
        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();

        h.Summary.SinceDate = new DateTimeOffset(Today.AddDays(-3));

        Assert.Equal(new DateTimeOffset(Today.AddDays(-3)), h.Summary.SinceDate);
        Assert.Equal(0, h.PostedCount);
        gate.Set();
        await h.Summary.PendingUsageReads;
        h.RunPosted();
        Assert.Equal(Bytes(300), h.Summary.TotalFromDateDownloadText);
    }

    [Fact]
    public async Task SetWidgetUsageFrom_ReadsOnlyTheWidgetTotal_AndTheLatestDateWins()
    {
        var h = new Harness((_, from, _) => (from == Today.AddDays(-3) ? 300 : from == Today.AddDays(-1) ? 100 : 5, 0));
        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();
        Assert.Equal(Bytes(5), h.Summary.TotalFromDateDownloadText);

        h.Summary.SetWidgetUsageFrom(Today.AddDays(-3), followToday: false);
        var first = h.Summary.PendingUsageReads;
        h.Summary.SetWidgetUsageFrom(Today.AddDays(-1), followToday: false);
        var second = h.Summary.PendingUsageReads;
        await Task.WhenAll(first, second);
        h.RunPosted();

        Assert.Equal(Today.AddDays(-1), h.Summary.WidgetUsageFromDate);
        Assert.Equal(Bytes(100), h.Summary.WidgetUsageDownloadText);
        // The Summary card has its own date and was not touched.
        Assert.Equal(Bytes(5), h.Summary.TotalFromDateDownloadText);
    }

    [Fact]
    public async Task RefreshAfterHistoryPurge_KeepsTheWidgetDateThatWasAskedFor()
    {
        var h = new Harness((_, from, _) => (from == Today.AddDays(-3) ? 300 : 5, 0));
        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();

        // The widget's new date has been asked for but its result hasn't been applied when the purge refresh starts.
        h.Summary.SetWidgetUsageFrom(Today.AddDays(-3), followToday: false);
        var widgetRead = h.Summary.PendingUsageReads;
        h.Summary.RefreshAfterHistoryPurge();
        await Task.WhenAll(widgetRead, h.Summary.PendingUsageReads);
        h.RunPosted();

        Assert.Equal(Today.AddDays(-3), h.Summary.WidgetUsageFromDate);
        Assert.Equal(Bytes(300), h.Summary.WidgetUsageDownloadText);
    }

    [Fact]
    public async Task RefreshAfterHistoryPurge_ReReadsEveryCardInOnePost()
    {
        var totals = (download: 1_000L, upload: 0L);
        var days = Daily(Today, 500, 50);
        var h = new Harness((_, _, _) => totals, (_, _, _) => days);
        h.Summary.SetActiveAdapter("Wi-Fi");
        await h.Summary.PendingUsageReads;
        h.RunPosted();
        Assert.Equal(Bytes(1_000), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(550), h.Summary.WeeklyTrend.TotalText);

        totals = (0, 0);
        days = [];
        h.Summary.RefreshAfterHistoryPurge();
        await h.Summary.PendingUsageReads;

        Assert.Equal(1, h.PostedCount);
        h.RunPosted();

        Assert.Equal(Bytes(0), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(0), h.Summary.WidgetUsageDownloadText);
        Assert.Equal(Bytes(0), h.Summary.WeeklyTrend.TotalText);
    }

    [Fact]
    public async Task WeeklyTrend_IsReReadInTheBackgroundEveryFifteenTicks()
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

        days = Daily(Today, 900, 100);
        for (var tick = 1; tick < 15; tick++)
            h.Flush();
        await h.Summary.PendingUsageReads;
        Assert.Equal(1, reads);

        // The fifteenth tick starts the read; it returns without waiting for it, and the bars change when it lands.
        h.Flush();
        await h.Summary.PendingUsageReads;
        Assert.Equal(2, reads);
        Assert.Equal(Bytes(550), h.Summary.WeeklyTrend.TotalText);

        h.RunPosted();
        Assert.Equal(Bytes(1_000), h.Summary.WeeklyTrend.TotalText);
    }

    [Fact]
    public async Task ClearOnDisconnect_DropsReadsThatBeganBeforeIt_ButKeepsTheWeeklyTrend()
    {
        var gate = new ManualResetEventSlim();
        var h = new Harness(
            (_, _, _) => { WaitFor(gate); return (1_000, 0); },
            (_, _, _) => Daily(Today, 500, 50));
        h.Flush(new ProcessTraffic("chrome", 100, 0));

        h.Summary.SetActiveAdapter("Wi-Fi");
        // The session ends (its counters restart) while the reads are still running.
        h.Summary.ClearOnDisconnect();
        gate.Set();
        await h.Summary.PendingUsageReads;
        h.RunPosted();

        // The totals were read against a session that no longer exists, so they are not applied; the frozen totals stay.
        Assert.Equal(Bytes(100), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(100), h.Summary.WidgetUsageDownloadText);
        // The weekly trend doesn't depend on the session, so it still lands.
        Assert.Equal(Bytes(550), h.Summary.WeeklyTrend.TotalText);
    }

    [Fact]
    public async Task ADisposedViewModelIgnoresResultsThatArriveLate()
    {
        var gate = new ManualResetEventSlim();
        var h = new Harness((_, _, _) => { WaitFor(gate); return (1_000, 0); });
        h.Summary.SetActiveAdapter("Wi-Fi");

        h.Summary.Dispose();
        gate.Set();
        await h.Summary.PendingUsageReads;
        h.RunPosted();

        Assert.Equal(Bytes(0), h.Summary.TotalFromDateDownloadText);
    }

    [Fact]
    public void WithoutAnAdapter_NothingIsReadAndEverythingIsAppliedRightAway()
    {
        var reads = 0;
        var h = new Harness((_, _, _) => { Interlocked.Increment(ref reads); return (9, 9); });

        // The startup path: the widget's date is set before any adapter is known.
        h.Summary.SetWidgetUsageFrom(Today.AddDays(-2), followToday: false);
        h.Summary.SinceDate = new DateTimeOffset(Today.AddDays(-1));

        Assert.Equal(0, reads);
        Assert.Equal(0, h.PostedCount);
        Assert.True(h.Summary.PendingUsageReads.IsCompleted);
        Assert.Equal(Today.AddDays(-2), h.Summary.WidgetUsageFromDate);
        Assert.Equal(Bytes(0), h.Summary.TotalFromDateDownloadText);
        Assert.Equal(Bytes(0), h.Summary.WidgetUsageDownloadText);
    }
}
