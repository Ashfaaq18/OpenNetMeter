using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

public class UsageSinceTrackerTests
{
    [Fact]
    public void Rebase_StartsFromDbTotalsAndAdvanceAddsSessionDelta()
    {
        var tracker = new UsageSinceTracker((_, _, _) => (1_000, 200));

        tracker.Rebase("Wi-Fi", DateTime.Today, sessionDownload: 500, sessionUpload: 50);
        Assert.Equal(1_000, tracker.Download);
        Assert.Equal(200, tracker.Upload);

        tracker.Advance(sessionDownload: 800, sessionUpload: 70);
        Assert.Equal(1_300, tracker.Download);
        Assert.Equal(220, tracker.Upload);
    }

    [Fact]
    public void Advance_NeverGoesBelowDbBaselineWhenSessionCountersReset()
    {
        var tracker = new UsageSinceTracker((_, _, _) => (1_000, 200));
        tracker.Rebase("Wi-Fi", DateTime.Today, 500, 50);

        tracker.Advance(0, 0);

        Assert.Equal(1_000, tracker.Download);
        Assert.Equal(200, tracker.Upload);
    }

    [Fact]
    public void Rebase_WithoutAdapterSkipsDbAndCountsOnlySession()
    {
        var dbRead = false;
        var tracker = new UsageSinceTracker((_, _, _) => { dbRead = true; return (9_999, 9_999); });

        tracker.Rebase("", DateTime.Today, 100, 10);
        tracker.Advance(150, 15);

        Assert.False(dbRead);
        Assert.Equal(50, tracker.Download);
        Assert.Equal(5, tracker.Upload);
    }

    [Fact]
    public void Rebase_QueriesFromDateThroughToday()
    {
        DateTime? from = null, to = null;
        var tracker = new UsageSinceTracker((_, f, t) => { from = f; to = t; return (0, 0); });
        var start = DateTime.Today.AddDays(-10).AddHours(13);

        tracker.Rebase("Wi-Fi", start, 0, 0);

        Assert.Equal(start.Date, from);
        Assert.Equal(DateTime.Today, to);
        Assert.Equal(start.Date, tracker.FromDate);
    }

    [Fact]
    public void Reset_ZeroesTotalsButKeepsFromDate()
    {
        var tracker = new UsageSinceTracker((_, _, _) => (1_000, 200));
        var start = DateTime.Today.AddDays(-3);
        tracker.Rebase("Wi-Fi", start, 0, 0);

        tracker.Reset();

        Assert.Equal(0, tracker.Download);
        Assert.Equal(0, tracker.Upload);
        Assert.Equal(start, tracker.FromDate);
    }

    [Fact]
    public void ReadDbTotals_SumsOnlyTheAdapterAndDateRange()
    {
        using var db = new TestUsageDb();
        var today = new DateTime(2026, 11, 12);
        db.AddUsage(today, "Wi-Fi", "a.exe", 100, 10);
        db.AddUsage(today.AddDays(-1), "Wi-Fi", "b.exe", 200, 20);
        db.AddUsage(today.AddDays(-5), "Wi-Fi", "a.exe", 400, 40);
        db.AddUsage(today, "Ethernet", "a.exe", 800, 80);

        var totals = UsageSinceTracker.ReadDbTotals(db.Path, "Wi-Fi", today.AddDays(-1), today);

        Assert.Equal((300L, 30L), totals);
    }

    [Fact]
    public void ReadDbTotals_ReturnsZeroWhenDatabaseIsMissing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"onm-missing-{Guid.NewGuid():N}.sqlite");

        Assert.Equal((0L, 0L), UsageSinceTracker.ReadDbTotals(missing, "Wi-Fi", DateTime.Today, DateTime.Today));
    }
}
