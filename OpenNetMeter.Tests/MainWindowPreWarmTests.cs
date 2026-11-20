using OpenNetMeter.Views;

namespace OpenNetMeter.Tests;

/// <summary>
/// The sequence that pays the main window's first-show cost invisibly (see MainWindowPreWarm), with a fake window:
/// when it is skipped, the order of the steps, what is restored when, and what happens if the user opens the window
/// meanwhile. Whether the DWM really keeps the window off screen can't be tested without a real window.
/// </summary>
public class MainWindowPreWarmTests
{
    private sealed class FakeHost : IPreWarmHost
    {
        private Action? framesDone;
        private Action? timeout;

        public List<string> Calls { get; } = [];
        public List<string> Failures { get; } = [];
        public bool HasBeenShown { get; set; }
        public bool IsVisible { get; set; }
        public bool CanCloak { get; set; } = true;
        public bool CanMoveOffScreen { get; set; } = true;
        public Exception? ThrowOnShow { get; set; }
        public Exception? ThrowOnHide { get; set; }
        public int FramesRequested { get; private set; }
        public TimeSpan TimeoutRequested { get; private set; }

        // The state the window would be in.
        public bool IgnoringWindowEvents { get; private set; }
        public bool Cloaked { get; private set; }
        public bool OffScreen { get; private set; }

        public void BeginWarmUp()
        {
            Calls.Add("BeginWarmUp");
            IgnoringWindowEvents = true;
        }

        public bool TryCloak()
        {
            Calls.Add("TryCloak");
            Cloaked = CanCloak;
            return CanCloak;
        }

        public bool TryMoveOffScreen()
        {
            Calls.Add("TryMoveOffScreen");
            OffScreen = CanMoveOffScreen;
            return CanMoveOffScreen;
        }

        public void Show()
        {
            Calls.Add("Show");
            IsVisible = true;
            if (ThrowOnShow is { } ex)
                throw ex;
        }

        public void WaitForFrames(int count, Action then)
        {
            Calls.Add($"WaitForFrames({count})");
            FramesRequested = count;
            framesDone = then;
        }

        public void ScheduleTimeout(TimeSpan delay, Action then)
        {
            Calls.Add("ScheduleTimeout");
            TimeoutRequested = delay;
            timeout = then;
        }

        public void Hide()
        {
            Calls.Add("Hide");
            if (ThrowOnHide is { } ex)
                throw ex;
            IsVisible = false;
        }

        public void Uncloak()
        {
            Calls.Add("Uncloak");
            Cloaked = false;
        }

        public void RestorePosition()
        {
            Calls.Add("RestorePosition");
            OffScreen = false;
        }

        public void EndWarmUp()
        {
            Calls.Add("EndWarmUp");
            IgnoringWindowEvents = false;
        }

        public void Completed() => Calls.Add("Completed");

        public void ShowNormally()
        {
            Calls.Add("ShowNormally");
            HasBeenShown = true;
            IsVisible = true;
        }

        public void LogFailure(string message, Exception ex) => Failures.Add(message);

        public void RenderFrames() => framesDone?.Invoke();
        public void FireTimeout() => timeout?.Invoke();
    }

    private static (FakeHost host, MainWindowPreWarm preWarm) Create(Action<FakeHost>? configure = null)
    {
        var host = new FakeHost();
        configure?.Invoke(host);
        return (host, new MainWindowPreWarm(host));
    }

    [Fact]
    public void ANormalLaunch_IsSkipped()
    {
        var (host, preWarm) = Create();

        Assert.False(preWarm.TryStart(startMinimized: false));

        Assert.Empty(host.Calls);
        Assert.False(preWarm.IsWarmingUp);
    }

    [Fact]
    public void IfTheUserOpenedTheWindowAlready_ItIsSkipped()
    {
        var (host, preWarm) = Create(h => h.HasBeenShown = true);

        Assert.False(preWarm.TryStart(startMinimized: true));

        Assert.Empty(host.Calls);
    }

    [Fact]
    public void IfTheWindowIsShowingRightNow_ItIsSkipped()
    {
        var (host, preWarm) = Create(h => h.IsVisible = true);

        Assert.False(preWarm.TryStart(startMinimized: true));

        Assert.Empty(host.Calls);
    }

    [Fact]
    public void TheSequence_CloaksThenShowsThenWaitsForTwoFrames_ThenHidesUncloaksAndRestores()
    {
        var (host, preWarm) = Create();

        Assert.True(preWarm.TryStart(startMinimized: true));

        // Cloaked before it is shown, and nothing hides it until two frames have been rendered.
        Assert.Equal(["BeginWarmUp", "TryCloak", "Show", "WaitForFrames(2)", "ScheduleTimeout"], host.Calls);
        Assert.True(preWarm.IsWarmingUp);
        Assert.True(host.IsVisible);
        Assert.True(host.Cloaked);
        Assert.True(host.IgnoringWindowEvents);
        Assert.Equal(2, host.FramesRequested);

        host.RenderFrames();

        Assert.Equal(
            ["BeginWarmUp", "TryCloak", "Show", "WaitForFrames(2)", "ScheduleTimeout", "Hide", "Uncloak", "EndWarmUp", "Completed"],
            host.Calls);
        Assert.False(preWarm.IsWarmingUp);
        Assert.False(host.IsVisible);
        Assert.False(host.Cloaked);
        Assert.False(host.IgnoringWindowEvents);
        // A pre-warm is not the user opening the window.
        Assert.False(host.HasBeenShown);
    }

    [Fact]
    public void WhenTheDwmRefusesToCloak_TheWindowIsKeptOffScreenInstead_AndPutBack()
    {
        var (host, preWarm) = Create(h => h.CanCloak = false);

        Assert.True(preWarm.TryStart(startMinimized: true));

        Assert.Equal(["BeginWarmUp", "TryCloak", "TryMoveOffScreen", "Show", "WaitForFrames(2)", "ScheduleTimeout"], host.Calls);
        Assert.True(host.OffScreen);

        host.RenderFrames();

        Assert.Equal(["Hide", "RestorePosition", "EndWarmUp", "Completed"], host.Calls.Skip(6));
        Assert.False(host.OffScreen);
        Assert.False(host.IgnoringWindowEvents);
    }

    [Fact]
    public void IfTheWindowCanNeitherBeCloakedNorMovedOffScreen_ItIsNeverShown()
    {
        var (host, preWarm) = Create(h =>
        {
            h.CanCloak = false;
            h.CanMoveOffScreen = false;
        });

        Assert.False(preWarm.TryStart(startMinimized: true));

        Assert.DoesNotContain("Show", host.Calls);
        Assert.Equal(["BeginWarmUp", "TryCloak", "TryMoveOffScreen", "EndWarmUp"], host.Calls);
        Assert.False(preWarm.IsWarmingUp);
        Assert.False(host.IgnoringWindowEvents);
        Assert.False(host.IsVisible);
    }

    [Fact]
    public void IfTheFramesNeverArrive_TheTimeoutFinishesIt()
    {
        var (host, preWarm) = Create();
        preWarm.TryStart(startMinimized: true);

        host.FireTimeout();

        Assert.False(preWarm.IsWarmingUp);
        Assert.False(host.IsVisible);
        Assert.False(host.Cloaked);
        Assert.Equal(MainWindowPreWarm.MaxWait, host.TimeoutRequested);
    }

    [Fact]
    public void TheFramesAndTheTimeoutBothFiring_FinishItOnce()
    {
        var (host, preWarm) = Create();
        preWarm.TryStart(startMinimized: true);

        host.RenderFrames();
        host.FireTimeout();
        host.RenderFrames();

        Assert.Equal(1, host.Calls.Count(c => c == "Hide"));
        Assert.Equal(1, host.Calls.Count(c => c == "Uncloak"));
        Assert.Equal(1, host.Calls.Count(c => c == "EndWarmUp"));
        Assert.Equal(1, host.Calls.Count(c => c == "Completed"));
    }

    [Fact]
    public void ItRunsOnlyOnce()
    {
        var (host, preWarm) = Create();
        preWarm.TryStart(startMinimized: true);
        host.RenderFrames();
        host.Calls.Clear();

        Assert.False(preWarm.TryStart(startMinimized: true));

        Assert.Empty(host.Calls);
    }

    [Fact]
    public void OpeningWhileItRuns_IsHeldUntilTheWindowIsHiddenAndRestored_ThenDoneNormally()
    {
        var (host, preWarm) = Create();
        preWarm.TryStart(startMinimized: true);

        preWarm.OpenForUser();
        preWarm.OpenForUser();

        // Nothing opened yet: the window is still the invisible one.
        Assert.DoesNotContain("ShowNormally", host.Calls);
        Assert.True(host.Cloaked);

        host.RenderFrames();

        // Finished cleanly first, then opened as the tray does (here twice, because it was asked twice; it is idempotent).
        var afterHide = host.Calls.SkipWhile(c => c != "Hide").ToList();
        Assert.Equal(["Hide", "Uncloak", "EndWarmUp", "Completed", "ShowNormally", "ShowNormally"], afterHide);
        Assert.False(host.Cloaked);
        Assert.False(host.IgnoringWindowEvents);
        Assert.True(host.IsVisible);
        Assert.True(host.HasBeenShown);
    }

    [Fact]
    public void OpeningWhenNoPreWarmIsRunning_IsDoneAtOnce()
    {
        var (host, preWarm) = Create();

        preWarm.OpenForUser();
        Assert.Equal(["ShowNormally"], host.Calls);

        // And after a finished pre-warm too.
        host.Calls.Clear();
        host.HasBeenShown = false;
        host.IsVisible = false;
        preWarm.TryStart(startMinimized: true);
        host.RenderFrames();
        host.Calls.Clear();

        preWarm.OpenForUser();
        Assert.Equal(["ShowNormally"], host.Calls);
    }

    [Fact]
    public void OtherWorkCanBeHeldUntilItHasFinished()
    {
        var (host, preWarm) = Create();
        preWarm.TryStart(startMinimized: true);
        var ran = false;

        preWarm.RunWhenIdle(() =>
        {
            ran = true;
            Assert.False(preWarm.IsWarmingUp);
            Assert.False(host.IgnoringWindowEvents);
        });

        Assert.False(ran);
        host.RenderFrames();
        Assert.True(ran);
    }

    [Fact]
    public void IfShowingThrows_TheWindowIsStillHiddenAndRestored()
    {
        var (host, preWarm) = Create(h => h.ThrowOnShow = new InvalidOperationException("no surface"));

        Assert.False(preWarm.TryStart(startMinimized: true));

        Assert.Equal(["Failed to pre-warm the main window"], host.Failures);
        Assert.Equal(["Hide", "Uncloak", "EndWarmUp"], host.Calls.Skip(3));
        Assert.False(preWarm.IsWarmingUp);
        Assert.False(host.Cloaked);
        Assert.False(host.IsVisible);
        Assert.False(host.IgnoringWindowEvents);
        // A failed attempt is not reported as a completed one.
        Assert.DoesNotContain("Completed", host.Calls);
    }

    [Fact]
    public void IfHidingThrows_TheRestoreStillRuns_SoTheWindowIsNeverLeftCloaked()
    {
        var (host, preWarm) = Create(h => h.ThrowOnHide = new InvalidOperationException("gone"));
        preWarm.TryStart(startMinimized: true);

        host.RenderFrames();

        Assert.Equal(["Failed to hide the pre-warmed main window"], host.Failures);
        Assert.False(host.Cloaked);
        Assert.False(host.IgnoringWindowEvents);
        Assert.False(preWarm.IsWarmingUp);
    }
}
