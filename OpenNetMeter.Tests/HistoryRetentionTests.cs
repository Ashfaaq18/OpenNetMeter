using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

public class HistoryRetentionTests
{
    private static readonly DateTime Today = new(2026, 10, 18);

    [Theory]
    [InlineData(60, 2026, 8, 19)]
    [InlineData(180, 2026, 4, 21)]
    [InlineData(365, 2025, 10, 18)]
    [InlineData(1825, 2021, 10, 19)]
    public void GetCutoff_ReturnsTodayMinusDays(int days, int year, int month, int day)
    {
        Assert.Equal(new DateTime(year, month, day), HistoryRetention.GetCutoff(Today, days));
    }

    [Fact]
    public void GetCutoff_ReturnsNullForForever()
    {
        Assert.Null(HistoryRetention.GetCutoff(Today, HistoryRetention.Forever));
    }

    [Fact]
    public void GetCutoff_TreatsNegativeAsDefault()
    {
        Assert.Equal(Today.AddDays(-HistoryRetention.DefaultDays), HistoryRetention.GetCutoff(Today, -5));
    }

    [Fact]
    public void GetCutoff_IgnoresTimeOfDay()
    {
        Assert.Equal(new DateTime(2026, 8, 19), HistoryRetention.GetCutoff(Today.AddHours(23), 60));
    }

    [Fact]
    public void IndexOf_MapsKnownAndUnknownValues()
    {
        for (int i = 0; i < HistoryRetention.Options.Length; i++)
            Assert.Equal(i, HistoryRetention.IndexOf(HistoryRetention.Options[i].Days));

        Assert.Equal(HistoryRetention.IndexOf(HistoryRetention.DefaultDays), HistoryRetention.IndexOf(90));
    }

    [Theory]
    [InlineData(HistoryRetention.Forever, HistoryRetention.Forever)]
    [InlineData(-1, 60)]
    [InlineData(1, 60)]
    [InlineData(60, 60)]
    [InlineData(90, 180)]
    [InlineData(200, 365)]
    [InlineData(1825, 1825)]
    [InlineData(2000, HistoryRetention.Forever)]
    public void Normalize_RoundsUpToAnOptionSoNoExtraHistoryIsDeleted(int stored, int expected)
    {
        Assert.Equal(expected, HistoryRetention.Normalize(stored));
    }

    [Theory]
    [InlineData(60, 180, true)]
    [InlineData(180, 60, false)]
    [InlineData(60, 60, false)]
    [InlineData(1825, HistoryRetention.Forever, true)]
    [InlineData(HistoryRetention.Forever, 60, false)]
    [InlineData(HistoryRetention.Forever, HistoryRetention.Forever, false)]
    public void IsShorter_TreatsForeverAsInfinite(int newDays, int oldDays, bool expected)
    {
        Assert.Equal(expected, HistoryRetention.IsShorter(newDays, oldDays));
    }
}
