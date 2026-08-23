using OpenNetMeter.Views.Controls.Charting;

namespace OpenNetMeter.Tests;

public class AxisScaleTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(37)]
    [InlineData(100)]
    [InlineData(999)]
    [InlineData(1000)]
    [InlineData(1234.5)]
    [InlineData(0.5)]
    [InlineData(9_999_999)]
    public void Compute_TopIsAlwaysAtLeastMax(double max)
    {
        var ticks = AxisScale.Compute(max, 2);

        Assert.True(ticks.Top >= max);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Compute_NonPositiveInputDoesNotThrow(double max)
    {
        var ticks = AxisScale.Compute(max, 2);

        Assert.True(ticks.Top > 0);
        Assert.True(ticks.Top >= max);
    }

    [Fact]
    public void Compute_StepIsANice1_2_5Multiple()
    {
        var ticks = AxisScale.Compute(37, 2);

        double step = ticks.Values[1] - ticks.Values[0];
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(step)));
        double normalized = Math.Round(step / magnitude, 6);

        Assert.Contains(normalized, new[] { 1.0, 2.0, 5.0, 10.0 });
    }

    [Fact]
    public void Compute_ValuesAreEvenlySpacedFromZero()
    {
        var ticks = AxisScale.Compute(1000, 2);

        Assert.Equal(3, ticks.Values.Count);
        Assert.Equal(0, ticks.Values[0]);
        double step = ticks.Values[1] - ticks.Values[0];
        Assert.Equal(step, ticks.Values[2] - ticks.Values[1], precision: 9);
        Assert.Equal(ticks.Top, ticks.Values[^1], precision: 9);
    }

    [Fact]
    public void Compute_DesiredIntervalsControlsValueCount()
    {
        var ticks = AxisScale.Compute(100, 4);

        Assert.Equal(5, ticks.Values.Count);
    }

    [Fact]
    public void Compute_ZeroOrNegativeIntervalsFallsBackToOne()
    {
        var ticks = AxisScale.Compute(100, 0);

        Assert.Equal(2, ticks.Values.Count);
    }
}
