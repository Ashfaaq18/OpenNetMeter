namespace OpenNetMeter.Views.Controls.Charting;

/// <summary>
/// Decides, for each display refresh, whether <see cref="SpeedChart"/> should redraw and whether its frame loop should
/// keep going. With software rendering every redraw costs CPU, so the chart redraws at about 30 fps instead of once per
/// refresh, and stops redrawing altogether once nothing is moving. Pure (no clock, no Avalonia), so it can be tested.
/// </summary>
internal static class ChartFramePacer
{
    /// <summary>
    /// The least time between two redraws while animating. A little under 33 ms (30 fps) on purpose: the chart's
    /// clock (<c>Environment.TickCount64</c>) advances in steps of about 15.6 ms, and with a 33 ms limit a 60 Hz display
    /// would round up to every third frame (20 fps).
    /// </summary>
    public const long MinFrameIntervalMs = 30;

    /// <summary>The "last draw" value for a chart that hasn't drawn yet: far enough back that the first frame draws.</summary>
    public const long NeverDrawnMs = long.MinValue / 2;

    public readonly record struct Decision(bool ShouldDraw, bool KeepRunning);

    /// <param name="nowMs">The current time on the same clock as the other timestamps.</param>
    /// <param name="lastSampleMs">When the newest sample arrived; the series glides one slot for <paramref name="slideDurationMs"/> after it.</param>
    /// <param name="maxTransitionStartMs">When the Y-axis maximum last started easing to a new value.</param>
    /// <param name="lastDrawMs">When the chart was last invalidated (<see cref="NeverDrawnMs"/> if never).</param>
    public static Decision Decide(
        long nowMs,
        long lastSampleMs,
        long maxTransitionStartMs,
        long lastDrawMs,
        double slideDurationMs,
        double maxEaseDurationMs)
    {
        bool slideFinished = nowMs - lastSampleMs >= slideDurationMs;
        bool easeFinished = nowMs - maxTransitionStartMs >= maxEaseDurationMs;

        // Both animations are over: one last frame, drawn whatever the frame limit, so the chart ends exactly at rest
        // (the render clamps the progress of a finished animation to 1). Nothing asks for a frame after it.
        if (slideFinished && easeFinished)
            return new Decision(ShouldDraw: true, KeepRunning: false);

        return new Decision(ShouldDraw: nowMs - lastDrawMs >= MinFrameIntervalMs, KeepRunning: true);
    }
}
