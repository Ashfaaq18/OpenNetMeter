using OpenNetMeter.Models;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

public class MiniWidgetUsageFromTests
{
    private static readonly DateTime Today = new(2026, 11, 12);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    [InlineData("12/11/2026")]
    public void Resolve_FollowsTodayForEmptyOrInvalidValues(string? stored)
    {
        Assert.Equal(Today, MiniWidgetUsageFrom.Resolve(stored, Today, 60));
    }

    [Fact]
    public void Resolve_ReturnsStoredDateWithinRetention()
    {
        Assert.Equal(new DateTime(2026, 11, 1), MiniWidgetUsageFrom.Resolve("2026-11-01", Today, 60));
    }

    [Fact]
    public void Resolve_ClampsToRetentionCutoff()
    {
        Assert.Equal(Today.AddDays(-60), MiniWidgetUsageFrom.Resolve("2025-01-01", Today, 60));
    }

    [Fact]
    public void Resolve_DoesNotClampWhenRetentionIsForever()
    {
        Assert.Equal(new DateTime(2020, 1, 1), MiniWidgetUsageFrom.Resolve("2020-01-01", Today, HistoryRetention.Forever));
    }

    [Fact]
    public void Resolve_ClampsFutureDatesToToday()
    {
        Assert.Equal(Today, MiniWidgetUsageFrom.Resolve("2027-01-01", Today, 60));
    }

    [Fact]
    public void ToStored_TodayMeansFollowToday()
    {
        var stored = MiniWidgetUsageFrom.ToStored(Today.AddHours(9), Today);

        Assert.Equal(MiniWidgetUsageFrom.FollowToday, stored);
        Assert.True(MiniWidgetUsageFrom.IsFollowToday(stored));
    }

    [Fact]
    public void ToStored_PastDateRoundTrips()
    {
        var date = new DateTime(2026, 10, 1);
        var stored = MiniWidgetUsageFrom.ToStored(date, Today);

        Assert.Equal("2026-10-01", stored);
        Assert.False(MiniWidgetUsageFrom.IsFollowToday(stored));
        Assert.Equal(date, MiniWidgetUsageFrom.Resolve(stored, Today, 60));
    }

    [Fact]
    public void FollowToday_ResolvesToTheNewDayAfterMidnight()
    {
        var stored = MiniWidgetUsageFrom.ToStored(Today, Today);

        Assert.Equal(Today.AddDays(1), MiniWidgetUsageFrom.Resolve(stored, Today.AddDays(1), 60));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(-1, 0)]
    public void NormalizeMode_FallsBackToSession(int mode, int expected)
    {
        Assert.Equal(expected, MiniWidgetUsageFrom.NormalizeMode(mode));
    }
}
