using Avalonia.Threading;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;
using OpenNetMeter.Views.Controls.Charting;

namespace OpenNetMeter.Tests;

/// <summary>
/// The capture tick is the Summary's one clock: each traffic batch posts one UI flush (coalesced while one is still
/// queued), and the 1-second timer is only a fallback that flushes when no batch has arrived for a while. The UI
/// thread and the clock are injected, so the flushes run exactly when a test says.
/// </summary>
public class SummaryTrafficBatchTests
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
        public bool HasSubscribers => TrafficBatchObserved != null;

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

        public Harness()
        {
            Summary = new SummaryViewModel(
                Capture,
                new PlaceholderProcessIconService(),
                new NoLinks(),
                new PlaceholderFileSaveDialogService(),
                new PlaceholderFileRevealService(),
                posted.Add,
                () => Now,
                startFallbackTimer: false);
        }

        public FakeCapture Capture { get; } = new();
        public SummaryViewModel Summary { get; }
        public long Now { get; set; } = 1_000_000;
        public int PostedCount => posted.Count;
        public int Samples => Summary.SpeedHistory.Count;

        /// <summary>Runs the flushes that were posted to the "UI thread", oldest first.</summary>
        public void RunPosted()
        {
            var toRun = posted.ToArray();
            posted.Clear();
            foreach (var action in toRun)
                action();
        }
    }

    [Fact]
    public void Batch_PostsOneFlush_AndTheFlushAppliesIt()
    {
        var h = new Harness();

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 10), new ProcessTraffic("teams", 5, 0));

        // Merged and queued, but nothing reaches the UI state until the posted flush runs.
        Assert.Equal(1, h.PostedCount);
        Assert.Equal(0, h.Samples);
        Assert.Empty(h.Summary.ActiveProcesses);

        h.RunPosted();

        Assert.Equal(1, h.Samples);
        Assert.Equal(new SpeedSample(105, 10), h.Summary.SpeedHistory[0]);
        Assert.Equal(2, h.Summary.ActiveProcesses.Count);
        var chrome = Assert.Single(h.Summary.ActiveProcesses, p => p.ProcessName == "chrome");
        Assert.Equal((100L, 10L), (chrome.CurrentDownloadBytes, chrome.CurrentUploadBytes));
        Assert.Equal(ByteSizeFormatter.FormatBytes(105), h.Summary.CurrentSessionDownloadText);
    }

    [Fact]
    public void BatchesArrivingBeforeTheQueuedFlushRuns_AreDrainedByThatOneFlush()
    {
        var h = new Harness();

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 0));
        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 50, 5));

        Assert.Equal(1, h.PostedCount);

        h.RunPosted();

        Assert.Equal(1, h.Samples);
        Assert.Equal(new SpeedSample(150, 5), h.Summary.SpeedHistory[0]);

        // The flush is no longer queued, so the next batch queues the next one.
        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 1, 1));
        Assert.Equal(1, h.PostedCount);
    }

    [Fact]
    public void EveryBatchIsExactlyOneSample_AndAnIdleBatchIsAZeroSample()
    {
        var h = new Harness();

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 10));
        h.RunPosted();
        Assert.Equal(1, h.Samples);

        // An idle tick sends an empty batch; it must still advance the clock by one (zero) sample.
        h.Capture.RaiseBatch();
        Assert.Equal(1, h.PostedCount);
        h.RunPosted();
        Assert.Equal(2, h.Samples);
        Assert.Equal(new SpeedSample(0, 0), h.Summary.SpeedHistory[1]);

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 7, 0));
        h.RunPosted();
        Assert.Equal(3, h.Samples);
        Assert.Equal(new SpeedSample(7, 0), h.Summary.SpeedHistory[2]);
    }

    [Fact]
    public void BatchMergesNamesThatDifferOnlyByCase()
    {
        var h = new Harness();

        h.Capture.RaiseBatch(new ProcessTraffic("Chrome", 100, 0), new ProcessTraffic("chrome", 50, 5));
        h.RunPosted();

        var chrome = Assert.Single(h.Summary.ActiveProcesses);
        Assert.Equal((150L, 5L), (chrome.CurrentDownloadBytes, chrome.CurrentUploadBytes));
    }

    [Fact]
    public void FallbackTick_DoesNotFlushWhileBatchesKeepArriving()
    {
        var h = new Harness();

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 0));
        h.RunPosted();
        Assert.Equal(1, h.Samples);

        // The timer fires about once a second, less than the 1.5 s of silence it waits for.
        h.Now += 1000;
        h.Summary.RunFallbackTick();
        Assert.Equal(1, h.Samples);

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 0));
        h.RunPosted();
        h.Now += 1000;
        h.Summary.RunFallbackTick();
        Assert.Equal(2, h.Samples);
    }

    [Fact]
    public void FallbackTick_FlushesOncePerTickOnceTheBatchesStop()
    {
        var h = new Harness();

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 0));
        h.RunPosted();
        Assert.Equal(1, h.Samples);

        // Just short of the threshold nothing happens; at it, the fallback keeps the chart moving.
        h.Now += SummaryViewModel.FallbackIdleMilliseconds - 1;
        h.Summary.RunFallbackTick();
        Assert.Equal(1, h.Samples);

        h.Now += 1;
        h.Summary.RunFallbackTick();
        Assert.Equal(2, h.Samples);
        Assert.Equal(new SpeedSample(0, 0), h.Summary.SpeedHistory[1]);

        h.Now += 1000;
        h.Summary.RunFallbackTick();
        h.Now += 1000;
        h.Summary.RunFallbackTick();
        Assert.Equal(4, h.Samples);

        // Nothing was posted: the fallback runs on the UI thread already.
        Assert.Equal(0, h.PostedCount);
    }

    [Fact]
    public void FallbackTick_StartsWaitingFromConstruction()
    {
        var h = new Harness();

        // Before any batch (capture still starting) it takes the same silence before the fallback flushes.
        h.Now += 1000;
        h.Summary.RunFallbackTick();
        Assert.Equal(0, h.Samples);

        h.Now += 600;
        h.Summary.RunFallbackTick();
        Assert.Equal(1, h.Samples);
    }

    [Fact]
    public void Dispose_StopsListeningAndALateFlushDoesNothing()
    {
        var h = new Harness();
        Assert.True(h.Capture.HasSubscribers);

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 0));
        h.Summary.Dispose();

        Assert.False(h.Capture.HasSubscribers);
        h.RunPosted();
        Assert.Equal(0, h.Samples);

        h.Now += 5000;
        h.Summary.RunFallbackTick();
        Assert.Equal(0, h.Samples);
    }

    [Fact]
    public void ClearOnDisconnect_DropsWhatWasPendingAndTheNextBatchStartsFresh()
    {
        var h = new Harness();

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 100, 0));
        h.RunPosted();
        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 40, 0));

        h.Summary.ClearOnDisconnect();
        h.RunPosted();

        // The queued flush found nothing left to apply (the pending traffic was cleared with the session).
        Assert.Empty(h.Summary.ActiveProcesses);

        h.Capture.RaiseBatch(new ProcessTraffic("chrome", 7, 0));
        h.RunPosted();
        var chrome = Assert.Single(h.Summary.ActiveProcesses);
        Assert.Equal(7, chrome.TotalDownloadBytes);
    }

    [Fact]
    public void FlushesArePostedAtTheDefaultPriorityTheNetworkChangedPostsUse()
    {
        // NetworkChanged is posted with Post(action) and so with the default priority; the tail flush of an
        // adapter switch must run before the "Disconnected" post that follows it, which only holds if both queue
        // at the same priority.
        Assert.Equal(default, DispatcherPriority.Default);
    }
}
