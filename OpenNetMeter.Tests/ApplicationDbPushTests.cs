using Microsoft.Data.Sqlite;
using OpenNetMeter.Models;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Tests;

/// <summary>
/// ApplicationDB keeps its connection, "today is set up" marker and clock in statics, so every test
/// that touches it runs serially in this collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ApplicationDbCollection
{
    public const string Name = "ApplicationDB static state";
}

[Collection(ApplicationDbCollection.Name)]
public class ApplicationDbPushTests
{
    private const string Adapter = "WiFi";
    private static readonly DateTime Day1 = new(2026, 11, 17);
    private static readonly DateTime Day2 = new(2026, 11, 18);

    [Fact]
    public void PushAfterMidnight_CreatesTheNewDayAndSavesUsage()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);

        scope.AtDay(Day1, db =>
        {
            db.CreateTable();
            db.InsertUniqueRow_AdapterTable(Adapter);
            db.UpdateDatesInDB();
            db.PushToDB("proc", 100, 10);
        });

        // The app kept running past midnight: nothing calls UpdateDatesInDB for the new day.
        scope.AtDay(Day2, db => db.PushToDB("proc", 200, 20));

        Assert.Equal(1, scope.DateRows(Day2));
        Assert.Equal((200L, 20L), scope.Usage(Day2, "proc"));
        Assert.Equal((100L, 10L), scope.Usage(Day1, "proc"));
    }

    [Fact]
    public void PushAfterMidnight_RunsRetentionPurge()
    {
        var oldDay = new DateTime(2026, 1, 1);
        using var scope = new ApplicationDbScope(retentionDays: 60);

        scope.AtDay(oldDay, db =>
        {
            db.CreateTable();
            db.InsertUniqueRow_AdapterTable(Adapter);
            db.UpdateDatesInDB();
            db.PushToDB("old.exe", 5, 1);
        });
        Assert.Equal(1, scope.DateRows(oldDay));

        // Day1 is well past the 60 day cutoff of the old row; only the push can trigger the purge.
        scope.AtDay(Day1, db => db.PushToDB("new.exe", 100, 10));

        Assert.Equal(0, scope.DateRows(oldDay));
        Assert.Equal(0, scope.Scalar("SELECT COUNT(*) FROM Process WHERE Name = 'old.exe'"));
        Assert.Equal(0, scope.Scalar("SELECT COUNT(*) FROM ProcessDate"
            + " WHERE DateID NOT IN (SELECT ID FROM Date)"));
        Assert.Equal(1, scope.DateRows(Day1));
        // The purge ran before the new process row was created, so it did not delete it.
        Assert.Equal((100L, 10L), scope.Usage(Day1, "new.exe"));
    }

    [Fact]
    public void PushTwiceOnTheSameDay_AddsUpAndDoesNotRerunThePurge()
    {
        var expiredDay = new DateTime(2020, 1, 1);
        using var scope = new ApplicationDbScope(retentionDays: 60);

        scope.AtDay(Day1, db =>
        {
            db.CreateTable();
            db.InsertUniqueRow_AdapterTable(Adapter);
            db.UpdateDatesInDB();
            db.PushToDB("proc", 100, 10);
        });

        // An expired row that appears after today was set up must survive a second push on the same day.
        scope.Execute($"INSERT INTO Date(Year, Month, Day) VALUES({expiredDay.Year}, {expiredDay.Month}, {expiredDay.Day})");

        scope.AtDay(Day1, db => db.PushToDB("proc", 100, 10));

        Assert.Equal((200L, 20L), scope.Usage(Day1, "proc"));
        Assert.Equal(1, scope.DateRows(expiredDay));
    }

    [Fact]
    public void CloseSharedConnection_MakesTheNextPushInsertTheDateRowAgain()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);

        scope.AtDay(Day1, db =>
        {
            db.CreateTable();
            db.InsertUniqueRow_AdapterTable(Adapter);
            db.UpdateDatesInDB();
            db.PushToDB("proc", 100, 10);
        });

        ApplicationDB.CloseSharedConnection();
        scope.Execute("DELETE FROM ProcessDate");
        scope.Execute("DELETE FROM Date");
        Assert.Equal(0, scope.DateRows(Day1));

        scope.AtDay(Day1, db => db.PushToDB("proc", 50, 5));

        Assert.Equal(1, scope.DateRows(Day1));
        Assert.Equal((50L, 5L), scope.Usage(Day1, "proc"));
    }

    /// <summary>
    /// Points ApplicationDB at a temporary database and restores every piece of static state on dispose.
    /// </summary>
    private sealed class ApplicationDbScope : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"onm-test-{Guid.NewGuid():N}.sqlite");
        private readonly Func<DateTime> originalToday = ApplicationDB.Today;
        private readonly string? originalPathOverride = ApplicationDB.DatabasePathOverride;
        private readonly int originalRetentionDays = SettingsManager.Current.HistoryRetentionDays;

        public ApplicationDbScope(int retentionDays)
        {
            // Also clears the "today is set up" marker left by another test.
            ApplicationDB.CloseSharedConnection();
            ApplicationDB.DatabasePathOverride = path;
            SettingsManager.Current.HistoryRetentionDays = retentionDays;
        }

        /// <summary>Runs <paramref name="work"/> on a fresh ApplicationDB, like one flush does, with the clock at <paramref name="day"/>.</summary>
        public void AtDay(DateTime day, Action<ApplicationDB> work)
        {
            ApplicationDB.Today = () => day;
            using var db = new ApplicationDB(Adapter);
            work(db);
        }

        public long Scalar(string sql)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        public void Execute(string sql)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public long DateRows(DateTime day) =>
            Scalar($"SELECT COUNT(*) FROM Date WHERE Year = {day.Year} AND Month = {day.Month} AND Day = {day.Day}");

        public (long Received, long Sent) Usage(DateTime day, string process)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT pd.DataReceived, pd.DataSent FROM ProcessDate pd " +
                "JOIN Process p ON p.ID = pd.ProcessID " +
                "JOIN Date d ON d.ID = pd.DateID " +
                $"WHERE p.Name = '{process}' AND d.Year = {day.Year} AND d.Month = {day.Month} AND d.Day = {day.Day}";
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read(), $"no usage row for '{process}' on {day:yyyy-MM-dd}");
            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        public void Dispose()
        {
            ApplicationDB.CloseSharedConnection();
            ApplicationDB.Today = originalToday;
            ApplicationDB.DatabasePathOverride = originalPathOverride;
            SettingsManager.Current.HistoryRetentionDays = originalRetentionDays;

            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
            {
                try { File.Delete(path + suffix); } catch (IOException) { }
            }
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            connection.Open();
            return connection;
        }
    }
}
