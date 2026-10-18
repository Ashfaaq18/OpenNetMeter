using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

public class RetentionPurgeTests
{
    [Fact]
    public void DeleteDatesBefore_RemovesOlderDaysAndCascadesUsageRows()
    {
        using var db = new TestUsageDb();
        db.AddUsage(new DateTime(2026, 1, 31), "WiFi", "old.exe", 10, 1);
        db.AddUsage(new DateTime(2026, 2, 1), "WiFi", "edge.exe", 20, 2);
        db.AddUsage(new DateTime(2026, 3, 15), "WiFi", "new.exe", 30, 3);

        int deleted = ApplicationDB.DeleteDatesBefore(db.Connection, new DateTime(2026, 2, 1));

        Assert.Equal(1, deleted);
        Assert.Equal(2, db.Scalar("SELECT COUNT(*) FROM Date"));
        Assert.Equal(2, db.Scalar("SELECT COUNT(*) FROM ProcessDate"));
        Assert.Equal(0, db.Scalar(
            "SELECT COUNT(*) FROM ProcessDate pd JOIN Process p ON p.ID = pd.ProcessID WHERE p.Name = 'old.exe'"));
    }

    [Fact]
    public void CreateSchema_IndexesProcessDateByDateForCascadingDeletes()
    {
        using var db = new TestUsageDb();

        Assert.Equal(1, db.Scalar(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_ProcessDate_DateID'"));
    }

    [Fact]
    public void DeleteDatesBefore_ComparesAcrossYearBoundary()
    {
        using var db = new TestUsageDb();
        db.AddUsage(new DateTime(2025, 12, 31), "WiFi", "a.exe", 1, 1);
        db.AddUsage(new DateTime(2026, 1, 1), "WiFi", "b.exe", 1, 1);

        ApplicationDB.DeleteDatesBefore(db.Connection, new DateTime(2026, 1, 1));

        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM ProcessDate"));
        Assert.Equal(2026, db.Scalar("SELECT Year FROM Date"));
    }
}
