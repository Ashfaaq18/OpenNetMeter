using OpenNetMeter.Views.Controls.Charting;

namespace OpenNetMeter.Tests;

/// <summary>
/// ChartFramePacer decides when SpeedChart redraws and when its frame loop stops. The chart itself needs a window, so
/// these drive the pacer with a fake display (a refresh every 1000/hz ms on a fake clock) the way SpeedChart.OnFrame does.
/// </summary>
public class ChartFramePacerTests
{
    // The chart's TickIntervalMs and MaxEaseDurationMs.
    private const double SlideMs = 1000;
    private const double EaseMs = 350;

    /// <summary>Mirrors SpeedChart's frame loop: it only gets display refreshes while it has asked for a frame.</summary>
    private sealed class FakeChart
    {
        private long lastSampleMs;
        private long maxTransitionStartMs;
        private long lastDrawMs = ChartFramePacer.NeverDrawnMs;

        public bool Running { get; private set; }
        public List<long> DrawTimes { get; } = [];
        public int FramesHandled { get; private set; }

        public FakeChart(long startMs)
        {
            // Attached: both animations start at attach time, like ResetAnimationState.
            lastSampleMs = startMs;
            maxTransitionStartMs = startMs;
            Running = true;
        }

        public void NewSample(long nowMs)
        {
            lastSampleMs = nowMs;
            Running = true;
        }

        public void NewScale(long nowMs)
        {
            maxTransitionStartMs = nowMs;
            Running = true;
        }

        /// <summary>One display refresh. Nothing happens while the loop is stopped (it returns a default decision).</summary>
        public ChartFramePacer.Decision Refresh(long nowMs)
        {
            if (!Running)
                return default;

            FramesHandled++;
            var decision = ChartFramePacer.Decide(nowMs, lastSampleMs, maxTransitionStartMs, lastDrawMs, SlideMs, EaseMs);
            if (decision.ShouldDraw)
            {
                lastDrawMs = nowMs;
                DrawTimes.Add(nowMs);
            }

            Running = decision.KeepRunning;
            return decision;
        }

        /// <summary>Refreshes at <paramref name="hz"/> from <paramref name="fromMs"/> up to (not including) <paramref name="toMs"/>.</summary>
        public void RunDisplay(int hz, long fromMs, long toMs)
        {
            for (int i = 0; ; i++)
            {
                long now = fromMs + (long)Math.Round(i * 1000.0 / hz);
                if (now >= toMs)
                    break;
                Refresh(now);
            }
        }
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void WhileSliding_DrawsAtMostOncePerFrameInterval(int hz)
    {
        var chart = new FakeChart(startMs: 10_000);

        chart.RunDisplay(hz, 10_000, 10_900);

        Assert.True(chart.DrawTimes.Count > 1);
        for (int i = 1; i < chart.DrawTimes.Count; i++)
            Assert.True(chart.DrawTimes[i] - chart.DrawTimes[i - 1] >= ChartFramePacer.MinFrameIntervalMs,
                $"draws at {chart.DrawTimes[i - 1]} and {chart.DrawTimes[i]} are closer than {ChartFramePacer.MinFrameIntervalMs} ms");
    }

    [Theory]
    [InlineData(60)]
    [InlineData(144)]
    public void WhileSliding_StillDrawsAboutThirtyTimesASecond(int hz)
    {
        var chart = new FakeChart(startMs: 10_000);

        chart.RunDisplay(hz, 10_000, 10_900);

        // 900 ms at ~30 fps is ~27 draws; far more means the cap isn't working, far less means the chart stutters.
        Assert.InRange(chart.DrawTimes.Count, 24, 31);
    }

    [Fact]
    public void TheFirstFrameAfterStartingDrawsImmediately()
    {
        var chart = new FakeChart(startMs: 10_000);

        var decision = chart.Refresh(10_000);

        Assert.True(decision.ShouldDraw);
        Assert.True(decision.KeepRunning);
    }

    [Fact]
    public void KeepsRunningThroughoutTheSlide()
    {
        var chart = new FakeChart(startMs: 0); // the max easing is long over; only the slide is moving
        chart.NewSample(10_000);

        for (long now = 10_000; now < 10_000 + (long)SlideMs; now += 7)
        {
            var decision = chart.Refresh(now);
            Assert.True(decision.KeepRunning, $"stopped {now - 10_000} ms into the slide");
        }
    }

    [Fact]
    public void KeepsRunningWhileTheMaxIsEasing_EvenAfterTheSlideEnded()
    {
        var chart = new FakeChart(startMs: 0);
        chart.NewSample(0);
        chart.NewScale(900); // slide ends at 1000, the new scale eases until 1250

        Assert.True(chart.Refresh(999).KeepRunning);
        Assert.True(chart.Refresh(1000).KeepRunning); // slide just ended, easing at 100/350
        Assert.True(chart.Refresh(1200).KeepRunning); // easing at 300/350

        var last = chart.Refresh(1250); // easing done
        Assert.False(last.KeepRunning);
        Assert.True(last.ShouldDraw);
    }

    [Fact]
    public void ShortScaleEasing_DoesNotKeepTheLoopAliveAfterTheSlide()
    {
        var chart = new FakeChart(startMs: 0);
        chart.NewSample(0);
        chart.NewScale(0); // eased by 350, long before the slide ends

        Assert.True(chart.Refresh(500).KeepRunning);
        Assert.False(chart.Refresh(1000).KeepRunning);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(144)]
    public void StopsAfterBothAnimationsFinish_WithExactlyOneFinalDrawAtRest(int hz)
    {
        var chart = new FakeChart(startMs: 0);
        chart.NewSample(0);
        chart.NewScale(200); // eases until 550, the slide until 1000

        chart.RunDisplay(hz, 0, 5_000);

        Assert.False(chart.Running);
        int drawsAtRest = chart.DrawTimes.Count(t => t >= 1000);
        Assert.Equal(1, drawsAtRest);

        // Nothing is drawn, and no frame is even handled, in the idle seconds after the final frame.
        long finalDraw = chart.DrawTimes[^1];
        Assert.True(finalDraw >= 1000);
        Assert.True(finalDraw < 1000 + 1000.0 / hz + 1, "the final frame was not the first one after the slide ended");
        int framesBeforeIdle = chart.FramesHandled;
        chart.RunDisplay(hz, 2_000, 5_000);
        Assert.Equal(framesBeforeIdle, chart.FramesHandled);
    }

    [Fact]
    public void TheFinalFrameAtRestIsDrawnEvenIfTheLastDrawWasJustBefore()
    {
        // The last draw was 5 ms ago (inside the frame limit), but the slide ended in between: the chart must still end exactly at rest.
        var decision = ChartFramePacer.Decide(
            nowMs: 11_000, lastSampleMs: 10_000, maxTransitionStartMs: 0, lastDrawMs: 10_995, SlideMs, EaseMs);

        Assert.True(decision.ShouldDraw);
        Assert.False(decision.KeepRunning);
    }

    [Fact]
    public void ANewSampleAfterTheLoopStopped_RestartsIt()
    {
        var chart = new FakeChart(startMs: 0);
        chart.NewSample(0);
        chart.RunDisplay(60, 0, 3_000);
        Assert.False(chart.Running);
        int drawsBefore = chart.DrawTimes.Count;

        chart.NewSample(3_000);
        var decision = chart.Refresh(3_017);

        Assert.True(chart.Running);
        Assert.True(decision.KeepRunning);
        Assert.True(decision.ShouldDraw);
        Assert.Equal(drawsBefore + 1, chart.DrawTimes.Count);

        // ...and it settles again after its own slide.
        chart.RunDisplay(60, 3_034, 6_000);
        Assert.False(chart.Running);
    }

    [Fact]
    public void ANewScaleAfterTheLoopStopped_RestartsIt()
    {
        var chart = new FakeChart(startMs: 0);
        chart.NewSample(0);
        chart.RunDisplay(60, 0, 3_000);
        Assert.False(chart.Running);

        chart.NewScale(3_000);
        var decision = chart.Refresh(3_017);

        Assert.True(chart.Running);
        Assert.True(decision.KeepRunning);

        chart.RunDisplay(60, 3_034, 6_000);
        Assert.False(chart.Running);
    }

    [Fact]
    public void WithASampleEverySecond_TheSlideNeverLetsTheLoopIdle()
    {
        // The slide lasts as long as the sample interval, so a live capture keeps the loop going (at the capped rate);
        // the idle stop pays off when samples stop arriving or arrive late.
        var chart = new FakeChart(startMs: 0);
        for (long second = 0; second < 10; second++)
        {
            chart.NewSample(second * 1000);
            chart.RunDisplay(60, second * 1000, second * 1000 + 999);
            Assert.True(chart.Running, $"loop idle during second {second}");
        }

        // 10 s at 60 Hz would be ~600 redraws uncapped; capped it is ~300.
        Assert.InRange(chart.DrawTimes.Count, 250, 310);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(31, true)]
    public void TheFrameIntervalBoundary(long msSinceLastDraw, bool expectedDraw)
    {
        long now = 10_500;
        var decision = ChartFramePacer.Decide(now, lastSampleMs: 10_000, maxTransitionStartMs: 0, lastDrawMs: now - msSinceLastDraw, SlideMs, EaseMs);

        Assert.Equal(expectedDraw, decision.ShouldDraw);
        Assert.True(decision.KeepRunning);
    }

    [Theory]
    [InlineData(999, true)]
    [InlineData(1000, false)]
    [InlineData(1001, false)]
    public void TheSlideEndsExactlyAtItsDuration(long msSinceSample, bool expectedKeepRunning)
    {
        var decision = ChartFramePacer.Decide(
            nowMs: 10_000 + msSinceSample, lastSampleMs: 10_000, maxTransitionStartMs: 0, lastDrawMs: 0, SlideMs, EaseMs);

        Assert.Equal(expectedKeepRunning, decision.KeepRunning);
    }

    [Theory]
    [InlineData(349, true)]
    [InlineData(350, false)]
    public void TheMaxEasingEndsExactlyAtItsDuration(long msSinceScaleChange, bool expectedKeepRunning)
    {
        var decision = ChartFramePacer.Decide(
            nowMs: 20_000 + msSinceScaleChange, lastSampleMs: 0, maxTransitionStartMs: 20_000, lastDrawMs: 0, SlideMs, EaseMs);

        Assert.Equal(expectedKeepRunning, decision.KeepRunning);
    }
}
