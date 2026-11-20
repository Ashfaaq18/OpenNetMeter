using Avalonia.Media;
using OpenNetMeter.Views.Controls.Charting;

namespace OpenNetMeter.Tests;

/// <summary>
/// SpeedChart renders every frame, so its shaped labels and pens are cached. These cover when the caches rebuild
/// (the chart itself needs a window to render, so the drawing is not unit-tested).
/// </summary>
public class SpeedChartCacheTests
{
    private sealed class Label(string text, LabelStyle style)
    {
        public string Text { get; } = text;
        public LabelStyle Style { get; } = style;
    }

    private readonly record struct LabelStyle(string Font, double Size, object? Brush);

    private static LabelCache<LabelStyle, Label> NewCache() => new((text, style) => new Label(text, style));

    private static readonly object BrushA = new();
    private static readonly object BrushB = new();

    [Fact]
    public void ALabelIsBuiltOnce_NoMatterHowManyFramesAskForIt()
    {
        var cache = NewCache();
        var style = new LabelStyle("Inter", 10, BrushA);

        var first = cache.Get(0, "1 KBps", style);
        for (var frame = 0; frame < 100; frame++)
            Assert.Same(first, cache.Get(0, "1 KBps", style));

        Assert.Equal(1, cache.BuildCount);
        Assert.Equal("1 KBps", first.Text);
    }

    [Fact]
    public void SlotsAreIndependent()
    {
        var cache = NewCache();
        var style = new LabelStyle("Inter", 10, BrushA);

        var zero = cache.Get(0, "0 Bps", style);
        var one = cache.Get(1, "512 Bps", style);
        var two = cache.Get(2, "1 KBps", style);

        Assert.Equal(3, cache.BuildCount);
        Assert.Same(zero, cache.Get(0, "0 Bps", style));
        Assert.Same(one, cache.Get(1, "512 Bps", style));
        Assert.Same(two, cache.Get(2, "1 KBps", style));
        Assert.Equal(3, cache.BuildCount);
    }

    [Fact]
    public void ChangedText_RebuildsOnlyThatSlot()
    {
        var cache = NewCache();
        var style = new LabelStyle("Inter", 10, BrushA);
        var zero = cache.Get(0, "0 Bps", style);
        var top = cache.Get(1, "1 KBps", style);

        var newTop = cache.Get(1, "2 KBps", style);

        Assert.NotSame(top, newTop);
        Assert.Equal("2 KBps", newTop.Text);
        Assert.Same(zero, cache.Get(0, "0 Bps", style));
        Assert.Equal(3, cache.BuildCount);
    }

    [Theory]
    [InlineData("OtherFont", 10, true)]
    [InlineData("Inter", 12, true)]
    [InlineData("Inter", 10, false)]
    public void AChangedStyle_RebuildsEverySlot(string font, double size, bool sameBrush)
    {
        var cache = NewCache();
        var original = new LabelStyle("Inter", 10, BrushA);
        var zero = cache.Get(0, "0 Bps", original);
        var top = cache.Get(1, "1 KBps", original);

        var changed = new LabelStyle(font, size, sameBrush ? BrushA : BrushB);
        var newZero = cache.Get(0, "0 Bps", changed);
        var newTop = cache.Get(1, "1 KBps", changed);

        Assert.NotSame(zero, newZero);
        Assert.NotSame(top, newTop);
        Assert.Equal(changed, newZero.Style);
        Assert.Equal(4, cache.BuildCount);

        // And it is stable again under the new style.
        Assert.Same(newZero, cache.Get(0, "0 Bps", changed));
        Assert.Equal(4, cache.BuildCount);
    }

    [Fact]
    public void GoingBackToTheOldStyle_RebuildsAgain_BecauseTheCacheHoldsOneStyle()
    {
        var cache = NewCache();
        var a = new LabelStyle("Inter", 10, BrushA);
        var b = new LabelStyle("Inter", 10, BrushB);

        cache.Get(0, "x", a);
        cache.Get(0, "x", b);
        cache.Get(0, "x", a);

        Assert.Equal(3, cache.BuildCount);
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var cache = NewCache();
        var style = new LabelStyle("Inter", 10, BrushA);
        var before = cache.Get(0, "x", style);

        cache.Clear();

        Assert.NotSame(before, cache.Get(0, "x", style));
        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public void ASlotBeyondTheCurrentCapacity_GrowsTheCache_WithoutLosingTheOthers()
    {
        var cache = NewCache();
        var style = new LabelStyle("Inter", 10, BrushA);
        var zero = cache.Get(0, "0", style);

        cache.Get(40, "40", style);

        Assert.Same(zero, cache.Get(0, "0", style));
        Assert.Equal(2, cache.BuildCount);
    }

    [Fact]
    public void APenIsBuiltOnce_ForTheSameBrushAndStroke()
    {
        var cache = new PenCache(3);
        var brush = new SolidColorBrush(Colors.Red);

        var first = cache.Get(1, brush, 2, PenLineCap.Round, PenLineJoin.Round);
        for (var frame = 0; frame < 100; frame++)
            Assert.Same(first, cache.Get(1, brush, 2, PenLineCap.Round, PenLineJoin.Round));

        Assert.Equal(1, cache.BuildCount);
        Assert.Same(brush, first.Brush);
        Assert.Equal(2, first.Thickness);
        Assert.Equal(PenLineCap.Round, first.LineCap);
        Assert.Equal(PenLineJoin.Round, first.LineJoin);
    }

    [Fact]
    public void APenIsRebuilt_WhenTheBrushInstanceThicknessCapOrJoinChanges()
    {
        var cache = new PenCache(1);
        var red = new SolidColorBrush(Colors.Red);
        var blue = new SolidColorBrush(Colors.Blue);

        var pen = cache.Get(0, red, 1);
        var newBrush = cache.Get(0, blue, 1);
        var newThickness = cache.Get(0, blue, 2);
        var newCap = cache.Get(0, blue, 2, PenLineCap.Round);
        var newJoin = cache.Get(0, blue, 2, PenLineCap.Round, PenLineJoin.Round);

        Assert.Equal(5, cache.BuildCount);
        Assert.Same(blue, newBrush.Brush);
        Assert.NotSame(pen, newBrush);
        Assert.NotSame(newBrush, newThickness);
        Assert.NotSame(newThickness, newCap);
        Assert.NotSame(newCap, newJoin);
    }

    [Fact]
    public void PenSlotsAreIndependent_SoTheGridAndBothSeriesKeepTheirOwn()
    {
        var cache = new PenCache(3);
        var brush = new SolidColorBrush(Colors.Red);

        var grid = cache.Get(0, brush, 1);
        var download = cache.Get(1, brush, 2, PenLineCap.Round, PenLineJoin.Round);
        var upload = cache.Get(2, brush, 2, PenLineCap.Round, PenLineJoin.Round);

        Assert.Equal(3, cache.BuildCount);
        Assert.NotSame(download, upload);
        Assert.Same(grid, cache.Get(0, brush, 1));
        Assert.Same(download, cache.Get(1, brush, 2, PenLineCap.Round, PenLineJoin.Round));
        Assert.Same(upload, cache.Get(2, brush, 2, PenLineCap.Round, PenLineJoin.Round));
        Assert.Equal(3, cache.BuildCount);
    }
}
