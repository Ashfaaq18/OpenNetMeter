using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

/// <summary>
/// The Summary page's values are only shown while the window is visible (not hidden, not minimized) and the Summary
/// tab is selected; the status bar's speed texts follow the window alone, as they show on every tab.
/// </summary>
public class SummaryActivityGateTests
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

        public event EventHandler<NetworkTrafficBatchEventArgs>? TrafficBatchObserved
        {
            add { }
            remove { }
        }

        public NetworkSnapshotChangedEventArgs? CurrentSnapshot => null;

        public void Start() { }
        public void Stop() { }
        public IReadOnlyList<NetworkAdapterOption> GetAvailableAdapters() => [];
        public void RefreshAdapterSelection() { }
        public void Dispose() { }
    }

    private static SummaryViewModel CreateSummary() => new(
        new FakeCapture(),
        new PlaceholderProcessIconService(),
        new NoLinks(),
        new PlaceholderFileSaveDialogService(),
        new PlaceholderFileRevealService(),
        _ => { },
        () => 1_000_000,
        startFallbackTimer: false);

    // What the view model currently holds back, found by seeing which notifications a refresh would raise.
    private static (bool page, bool speed) Observe(SummaryViewModel summary, Action change)
    {
        var raised = new List<string?>();
        summary.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        change();
        return (raised.Contains(nameof(SummaryViewModel.ProcessCount)), raised.Contains(nameof(SummaryViewModel.DownloadSpeedText)));
    }

    [Fact]
    public void StartsInactive_BecauseTheWindowIsNotShownYet()
    {
        var summary = CreateSummary();
        _ = new SummaryActivityGate(summary);

        // Activating now raises the page and speed refreshes, which means both were off.
        var (page, speed) = Observe(summary, () =>
        {
            summary.SetUiActive(true);
            summary.SetSpeedActive(true);
        });

        Assert.True(page);
        Assert.True(speed);
    }

    [Fact]
    public void ShowingTheWindow_ActivatesThePageOnTheSummaryTab_AndTheSpeedTexts()
    {
        var summary = CreateSummary();
        var gate = new SummaryActivityGate(summary);

        var (page, speed) = Observe(summary, () => gate.SetWindowActive(true));

        Assert.True(page);
        Assert.True(speed);
    }

    [Fact]
    public void ShowingTheWindowOnAnotherTab_ActivatesOnlyTheSpeedTexts()
    {
        var summary = CreateSummary();
        var gate = new SummaryActivityGate(summary);
        gate.SetSummaryTabSelected(false);

        var (page, speed) = Observe(summary, () => gate.SetWindowActive(true));

        Assert.False(page);
        Assert.True(speed);
    }

    [Fact]
    public void SelectingTheSummaryTab_ActivatesThePage_WhileTheWindowIsShowing()
    {
        var summary = CreateSummary();
        var gate = new SummaryActivityGate(summary);
        gate.SetSummaryTabSelected(false);
        gate.SetWindowActive(true);

        var (page, speed) = Observe(summary, () => gate.SetSummaryTabSelected(true));

        Assert.True(page);
        Assert.False(speed);
    }

    [Fact]
    public void SelectingTheSummaryTab_DoesNothingWhileTheWindowIsHidden()
    {
        var summary = CreateSummary();
        var gate = new SummaryActivityGate(summary);

        var (page, speed) = Observe(summary, () =>
        {
            gate.SetSummaryTabSelected(false);
            gate.SetSummaryTabSelected(true);
        });

        Assert.False(page);
        Assert.False(speed);
    }

    [Fact]
    public void HidingOrMinimizingTheWindow_DeactivatesBoth()
    {
        var summary = CreateSummary();
        var gate = new SummaryActivityGate(summary);
        gate.SetWindowActive(true);
        gate.SetWindowActive(false);

        // Both are off again: turning them on raises the refreshes.
        var (page, speed) = Observe(summary, () => gate.SetWindowActive(true));

        Assert.True(page);
        Assert.True(speed);
    }
}
