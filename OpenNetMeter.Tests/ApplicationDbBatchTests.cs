using Microsoft.Data.Sqlite;
using OpenNetMeter.Models;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Tests;

/// <summary>
/// The batched flush: one transaction per push, cached ids, and a long-lived connection. All of it lives in
/// ApplicationDB statics, so these run serially, against a temporary database only.
/// </summary>
[Collection(ApplicationDbCollection.Name)]
public class ApplicationDbBatchTests
{
    private const string Adapter = ApplicationDbScope.Adapter;
    private static readonly DateTime Day1 = new(2026, 11, 17);

    // ---- Batch writes ----

    [Fact]
    public void PushBatch_AddsUpAcrossFlushes_AndMatchesThePerRowStatements()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);

        // The statements the per-row push ran, applied to a second database as the reference.
        using var reference = new TestUsageDb();
        reference.AddDate(Day1);

        void Flush(params (string Name, long Received, long Sent)[] rows)
        {
            db.PushBatch(Adapter, rows);
            foreach (var (name, received, sent) in rows)
                reference.OldPerRowPush(Adapter, Day1, name, received, sent);
        }

        Flush(("chrome", 100, 10), ("teams", 5, 1));
        Flush(("chrome", 50, 5), ("code", 7, 7));
        Flush(("chrome", 1, 1));
        Flush(("teams", 2, 2), ("code", 3, 3), ("steam", 40_000_000_000, 9));
        Flush(("chrome", 0, 0));

        Assert.Equal((151L, 16L), scope.Usage(Day1, "chrome"));
        Assert.Equal((7L, 3L), scope.Usage(Day1, "teams"));
        Assert.Equal((10L, 10L), scope.Usage(Day1, "code"));
        Assert.Equal((40_000_000_000L, 9L), scope.Usage(Day1, "steam"));
        Assert.Equal(reference.UsageRows(), UsageRows(scope));
    }

    [Fact]
    public void PushBatch_EmptyBatchWritesNothing()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);

        db.PushBatch(Adapter, []);

        Assert.Equal(0, scope.Scalar("SELECT COUNT(*) FROM ProcessDate"));
        Assert.Equal(0, scope.Scalar("SELECT COUNT(*) FROM Process"));
    }

    [Fact]
    public void PushBatch_ProcessNamesAreCaseSensitiveLikeTheTable()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);

        db.PushBatch(Adapter, [("Chrome", 1, 1), ("chrome", 10, 10)]);
        db.PushBatch(Adapter, [("Chrome", 1, 1)]);

        Assert.Equal((2L, 2L), scope.Usage(Day1, "Chrome"));
        Assert.Equal((10L, 10L), scope.Usage(Day1, "chrome"));
    }

    [Fact]
    public void PushBatch_NewProcessInALaterFlushGetsAnId()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);

        db.PushBatch(Adapter, [("chrome", 100, 10)]);
        db.PushBatch(Adapter, [("chrome", 1, 1), ("teams", 30, 3)]);

        Assert.Equal(["chrome", "teams"], scope.ProcessNames());
        Assert.Equal((101L, 11L), scope.Usage(Day1, "chrome"));
        Assert.Equal((30L, 3L), scope.Usage(Day1, "teams"));
    }

    [Fact]
    public void PushBatch_AnotherAdapterKeepsItsOwnRows()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);

        db.PushBatch(Adapter, [("chrome", 100, 10)]);
        db.PushBatch("Ethernet", [("chrome", 5, 1)]);
        db.PushBatch(Adapter, [("chrome", 1, 1)]);

        Assert.Equal(2, scope.Scalar("SELECT COUNT(*) FROM Adapter"));
        Assert.Equal(2, scope.Scalar("SELECT COUNT(*) FROM ProcessDate"));
        Assert.Equal(101, scope.Scalar(
            "SELECT pd.DataReceived FROM ProcessDate pd JOIN Adapter a ON a.ID = pd.AdapterID WHERE a.Name = 'WiFi'"));
        Assert.Equal(5, scope.Scalar(
            "SELECT pd.DataReceived FROM ProcessDate pd JOIN Adapter a ON a.ID = pd.AdapterID WHERE a.Name = 'Ethernet'"));
    }

    [Fact]
    public void PushBatch_AfterMidnightUsesTheNewDaysId()
    {
        var day2 = Day1.AddDays(1);
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);

        db.PushBatch(Adapter, [("chrome", 100, 10)]);
        ApplicationDB.Today = () => day2;
        db.PushBatch(Adapter, [("chrome", 7, 1)]);

        Assert.Equal((100L, 10L), scope.Usage(Day1, "chrome"));
        Assert.Equal((7L, 1L), scope.Usage(day2, "chrome"));
    }

    [Fact]
    public void UpsertStatement_OnTheRealSchema_AddsToTheExistingRowInsteadOfIgnoringIt()
    {
        // The table declares UNIQUE(ProcessID, DateID, AdapterID) ON CONFLICT IGNORE; the upsert must still update.
        using var db = new TestUsageDb();
        db.AddUsage(Day1, "WiFi", "chrome", 100, 10);
        db.AddUsage(Day1, "Ethernet", "chrome", 5, 5);

        db.Upsert("chrome", Day1, "WiFi", 50, 5);
        db.Upsert("chrome", Day1, "WiFi", 1, 2);
        db.Upsert("steam", Day1, "WiFi", 9, 9);

        Assert.Equal(3, db.Scalar("SELECT COUNT(*) FROM ProcessDate"));
        Assert.Equal(151, db.Scalar(
            "SELECT pd.DataReceived FROM ProcessDate pd JOIN Process p ON p.ID = pd.ProcessID " +
            "JOIN Adapter a ON a.ID = pd.AdapterID WHERE p.Name = 'chrome' AND a.Name = 'WiFi'"));
        Assert.Equal(17, db.Scalar(
            "SELECT pd.DataSent FROM ProcessDate pd JOIN Process p ON p.ID = pd.ProcessID " +
            "JOIN Adapter a ON a.ID = pd.AdapterID WHERE p.Name = 'chrome' AND a.Name = 'WiFi'"));
        Assert.Equal(5, db.Scalar(
            "SELECT pd.DataReceived FROM ProcessDate pd JOIN Adapter a ON a.ID = pd.AdapterID WHERE a.Name = 'Ethernet'"));
    }

    // ---- Id caches ----

    [Fact]
    public void IdCaches_AreInvalidatedByCloseSharedConnection()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        db.PushBatch(Adapter, [("chrome", 100, 10)]);

        // Like Delete All with a session still holding its instance, except the file survives so the ids can be
        // reused: every table is emptied and decoys take over the ids the cache remembers.
        ApplicationDB.CloseSharedConnection();
        scope.Execute("DELETE FROM ProcessDate");
        scope.Execute("DELETE FROM Process");
        scope.Execute("DELETE FROM Adapter");
        scope.Execute("DELETE FROM Date");
        scope.Execute("INSERT INTO Adapter(Name) VALUES('decoy-adapter')");
        scope.Execute("INSERT INTO Process(Name) VALUES('decoy-process')");

        db.PushBatch(Adapter, [("chrome", 50, 5)]);

        Assert.Equal((50L, 5L), scope.Usage(Day1, "chrome"));
        Assert.Null(scope.TryUsage(Day1, "decoy-process"));
        Assert.Equal(1, scope.Scalar(
            "SELECT COUNT(*) FROM ProcessDate pd JOIN Adapter a ON a.ID = pd.AdapterID WHERE a.Name = 'WiFi'"));
    }

    [Fact]
    public void IdCaches_AreInvalidatedWhenAPurgeDeletesProcesses()
    {
        var oldDay = new DateTime(2026, 1, 1);
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(oldDay);
        db.PushBatch(Adapter, [("old.exe", 5, 1)]);

        // The session runs on to a day on which the old row is past retention. The purge deletes old.exe, and
        // new.exe is then inserted under the very id old.exe had.
        ApplicationDB.Today = () => Day1;
        db.PushBatch(Adapter, [("new.exe", 100, 10)]);
        Assert.Equal(["new.exe"], scope.ProcessNames());

        // A stale cached id for old.exe would now credit new.exe's row.
        db.PushBatch(Adapter, [("old.exe", 7, 7)]);

        Assert.Equal((7L, 7L), scope.Usage(Day1, "old.exe"));
        Assert.Equal((100L, 10L), scope.Usage(Day1, "new.exe"));
    }

    [Fact]
    public void IdCaches_AreInvalidatedWhenTheExplicitPurgeDeletesProcesses()
    {
        using var scope = new ApplicationDbScope(retentionDays: 0);
        using var db = scope.Start(Day1);
        db.PushBatch(Adapter, [("chrome", 100, 10)]);

        // Settings: retention shortened. The only ProcessDate row is expired, so chrome is purged too.
        SettingsManager.Current.HistoryRetentionDays = 60;
        ApplicationDB.Today = () => Day1.AddDays(400);
        ApplicationDB.ApplyRetention();
        Assert.Empty(scope.ProcessNames());

        db.PushBatch(Adapter, [("steam", 1, 1)]);
        db.PushBatch(Adapter, [("chrome", 3, 3)]);

        Assert.Equal((1L, 1L), scope.Usage(Day1.AddDays(400), "steam"));
        Assert.Equal((3L, 3L), scope.Usage(Day1.AddDays(400), "chrome"));
    }

    // ---- Failure and connection lifetime ----

    [Fact]
    public void PushBatch_ThatFailsPartwayWritesNothingAndForgetsTheIdsItCreated()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        db.PushBatch(Adapter, [("chrome", 100, 10)]);
        // The second row of the batch fails, after the first one already inserted its Process row.
        scope.Execute(
            "CREATE TRIGGER fail_on_boom BEFORE INSERT ON ProcessDate WHEN NEW.DataReceived = 666 " +
            "BEGIN SELECT RAISE(ABORT, 'boom'); END");

        Assert.ThrowsAny<SqliteException>(() => db.PushBatch(Adapter, [("brand-new", 1, 1), ("boomer", 666, 1)]));

        Assert.Equal(["chrome"], scope.ProcessNames());
        Assert.Equal(1, scope.Scalar("SELECT COUNT(*) FROM ProcessDate"));

        // brand-new's rolled-back id is free again; a cached copy of it would send brand-new's usage to "other".
        scope.Execute("DROP TRIGGER fail_on_boom");
        db.PushBatch(Adapter, [("other", 2, 2), ("brand-new", 3, 3)]);

        Assert.Equal((2L, 2L), scope.Usage(Day1, "other"));
        Assert.Equal((3L, 3L), scope.Usage(Day1, "brand-new"));
        Assert.Equal((100L, 10L), scope.Usage(Day1, "chrome"));
    }

    [Fact]
    public void FailedPush_IsQueuedWithItsAdapterAndWrittenByTheNextPush()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess();
        var errors = new List<string>();
        netProc.DbWriteErrorLogger = (message, _) => errors.Add(message);
        netProc.TestUseDatabase(Adapter, db);

        Stage(netProc, ("chrome", 100, 10));
        netProc.TestPushPendingToDb();
        Assert.Equal((100L, 10L), scope.Usage(Day1, "chrome"));
        Assert.Empty(netProc.PushToDBBuffer);

        // Break the database, then push: the batch leaves the buffer and waits in the retry queue instead of being lost.
        scope.Execute("DROP TABLE ProcessDate");
        Stage(netProc, ("chrome", 50, 5), ("teams", 1, 1));
        netProc.TestPushPendingToDb();

        Assert.Single(errors);
        Assert.Empty(netProc.PushToDBBuffer);
        var queued = Assert.Single(netProc.TestQueuedBatches());
        Assert.Equal(Adapter, queued.Key);
        Assert.Equal((50L, 5L), queued.Value["chrome"]);
        Assert.Equal((1L, 1L), queued.Value["teams"]);

        // A repeated failure is not logged again, and what failed again sums into the same queue entry.
        Stage(netProc, ("chrome", 1, 1));
        netProc.TestPushPendingToDb();

        Assert.Single(errors);
        Assert.Equal((51L, 6L), Assert.Single(netProc.TestQueuedBatches()).Value["chrome"]);

        // Repair the database: the next push writes everything that was waiting.
        db.CreateTable();
        netProc.TestPushPendingToDb();

        Assert.Empty(netProc.PushToDBBuffer);
        Assert.Empty(netProc.TestQueuedBatches());
        Assert.Equal((51L, 6L), scope.Usage(Day1, "chrome"));
        Assert.Equal((1L, 1L), scope.Usage(Day1, "teams"));
        Assert.Single(errors);

        // A failure after a recovery is a new streak and is logged again.
        scope.Execute("DROP TABLE ProcessDate");
        Stage(netProc, ("chrome", 1, 1));
        netProc.TestPushPendingToDb();
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void FailedPush_KeepsItsAdapterWhenTheAdapterChangesBeforeTheRetry()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess();
        netProc.DbWriteErrorLogger = (_, _) => { };
        netProc.TestUseDatabase("WiFi", db);

        // The write on WiFi fails.
        scope.Execute("DROP TABLE ProcessDate");
        Stage(netProc, ("chrome", 50, 5), ("teams", 1, 1));
        netProc.TestPushPendingToDb();
        Assert.Equal(["WiFi"], netProc.TestQueuedBatches().Keys);

        // The database recovers and capture moves to Ethernet before the next push.
        db.CreateTable();
        netProc.TestUseDatabase("Ethernet", db);
        Stage(netProc, ("chrome", 7, 7));
        netProc.TestPushPendingToDb();

        Assert.Empty(netProc.TestQueuedBatches());
        Assert.Equal((50L, 5L), UsageFor(scope, "WiFi", "chrome"));
        Assert.Equal((1L, 1L), UsageFor(scope, "WiFi", "teams"));
        Assert.Equal((7L, 7L), UsageFor(scope, "Ethernet", "chrome"));
        Assert.Null(UsageFor(scope, "Ethernet", "teams"));
    }

    [Fact]
    public void FailedPushes_AreCoalescedPerAdapterAndProcess()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess();
        var errors = new List<string>();
        netProc.DbWriteErrorLogger = (message, _) => errors.Add(message);
        netProc.TestUseDatabase("WiFi", db);
        scope.Execute("DROP TABLE ProcessDate");

        // However many pushes fail, the queue holds one entry per (adapter, process).
        for (int i = 0; i < 5; i++)
        {
            Stage(netProc, ("chrome", 1, 1), ("teams", 10, 10));
            netProc.TestPushPendingToDb();
        }

        var queued = Assert.Single(netProc.TestQueuedBatches());
        Assert.Equal(2, queued.Value.Count);
        Assert.Equal((5L, 5L), queued.Value["chrome"]);
        Assert.Equal((50L, 50L), queued.Value["teams"]);

        netProc.TestUseDatabase("Ethernet", db);
        Stage(netProc, ("chrome", 3, 3));
        netProc.TestPushPendingToDb();
        netProc.TestPushPendingToDb();

        var both = netProc.TestQueuedBatches();
        Assert.Equal(["Ethernet", "WiFi"], both.Keys.Order());
        Assert.Equal(2, both["WiFi"].Count);
        Assert.Equal((3L, 3L), Assert.Single(both["Ethernet"]).Value);
        Assert.Single(errors);

        db.CreateTable();
        netProc.TestPushPendingToDb();

        Assert.Empty(netProc.TestQueuedBatches());
        Assert.Equal((5L, 5L), UsageFor(scope, "WiFi", "chrome"));
        Assert.Equal((50L, 50L), UsageFor(scope, "WiFi", "teams"));
        Assert.Equal((3L, 3L), UsageFor(scope, "Ethernet", "chrome"));
    }

    [Fact]
    public void FailedPush_IsRetriedByTheNextSessionAfterStopAndStart()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess();
        var errors = new List<string>();
        netProc.DbWriteErrorLogger = (message, _) => errors.Add(message);
        netProc.TestUseDatabase("WiFi", db);

        scope.Execute("DROP TABLE ProcessDate");
        Stage(netProc, ("chrome", 50, 5));
        netProc.TestPushPendingToDb();

        // Stop: the final flush also fails, and it queues what was still staged. The session lets go of the database.
        Stage(netProc, ("teams", 1, 1));
        netProc.EndNetworkProcess();

        Assert.False(netProc.TestHasDatabase);
        var waiting = Assert.Single(netProc.TestQueuedBatches());
        Assert.Equal(["chrome", "teams"], waiting.Value.Keys.Order());

        // Between sessions a push has nothing to write to: the queue just waits.
        netProc.TestPushPendingToDb();
        Assert.Equal(2, Assert.Single(netProc.TestQueuedBatches()).Value.Count);

        // Start on another adapter: the database is back, and the first push settles the old adapter's usage first.
        using var nextSession = scope.Open(Day1);
        nextSession.CreateTable();
        netProc.TestUseDatabase("Ethernet", nextSession);
        Stage(netProc, ("chrome", 7, 7));
        netProc.TestPushPendingToDb();

        Assert.Empty(netProc.TestQueuedBatches());
        Assert.Equal((50L, 5L), UsageFor(scope, "WiFi", "chrome"));
        Assert.Equal((1L, 1L), UsageFor(scope, "WiFi", "teams"));
        Assert.Equal((7L, 7L), UsageFor(scope, "Ethernet", "chrome"));
        Assert.Single(errors);
    }

    [Fact]
    public void ConnectionStaysOpenBetweenFlushesAndClosesWhenTheSessionEnds()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess();
        netProc.DbWriteErrorLogger = (message, _) => Assert.Fail(message);
        netProc.TestUseDatabase(Adapter, db);

        Stage(netProc, ("chrome", 100, 10));
        netProc.TestPushPendingToDb();
        Assert.True(ApplicationDB.IsConnectionOpen);

        Stage(netProc, ("chrome", 1, 1));
        netProc.TestPushPendingToDb();
        Assert.True(ApplicationDB.IsConnectionOpen);

        // The final flush on stop writes what is still staged, then the session lets go of the connection.
        Stage(netProc, ("chrome", 2, 2));
        netProc.EndNetworkProcess();

        Assert.Equal((103L, 13L), scope.Usage(Day1, "chrome"));
        Assert.False(netProc.TestHasDatabase);
        Assert.False(ApplicationDB.IsConnectionOpen);
    }

    [Fact]
    public void ClosingTheConnectionAndThePools_CheckpointsTheWal()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using (var db = scope.Start(Day1))
            db.PushBatch(Adapter, [("chrome", 100, 10)]);

        // Disposing the last instance only returns the connection to the pool; its WAL is still there.
        var wal = scope.Path + "-wal";
        Assert.True(File.Exists(wal) && new FileInfo(wal).Length > 0, "expected unflushed WAL frames");

        // What App's exit handler does: the pooled handle closes, SQLite checkpoints, and nothing is left to replay.
        ApplicationDB.CloseSharedConnection();
        SqliteConnection.ClearAllPools();

        Assert.True(!File.Exists(wal) || new FileInfo(wal).Length == 0, "the WAL should be checkpointed away on close");
        Assert.Equal((100L, 10L), scope.Usage(Day1, "chrome"));
    }

    [Fact]
    public void ADisposedSingleFlushInstanceStillClosesTheConnection()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);

        scope.AtDay(Day1, db => db.CreateTable());

        Assert.False(ApplicationDB.IsConnectionOpen);
    }

    [Fact]
    public void ASessionInstanceSurvivesDeleteAll_ItReopensAndRebuildsTheSchema()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        db.PushBatch(Adapter, [("chrome", 100, 10)]);

        // History.DeleteAllDbFiles: close the shared connection, drop the pools, delete the files.
        scope.DeleteFiles();
        Assert.False(File.Exists(scope.Path));

        db.PushBatch(Adapter, [("chrome", 50, 5)]);

        Assert.Equal((50L, 5L), scope.Usage(Day1, "chrome"));
        Assert.True(ApplicationDB.IsConnectionOpen);
    }

    [Fact]
    public void ApplyRetention_WhileASessionHoldsTheConnection_LeavesItOpen()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        db.PushBatch(Adapter, [("chrome", 100, 10)]);

        ApplicationDB.ApplyRetention();

        Assert.True(ApplicationDB.IsConnectionOpen);
        db.PushBatch(Adapter, [("chrome", 1, 1)]);
        Assert.Equal((101L, 11L), scope.Usage(Day1, "chrome"));
    }

    // ---- Indexes ----

    [Fact]
    public void CreateSchema_IndexesProcessDateByAdapterAndDate()
    {
        using var db = new TestUsageDb();

        Assert.Equal(1, IndexCount(db.Connection, "IX_ProcessDate_AdapterID_DateID"));
    }

    [Fact]
    public void CreateTable_AddsTheIndexToAnOldDatabase()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        UseOldSchema(scope);
        Assert.Equal(0, scope.Scalar(IndexQuery));

        scope.AtDay(Day1, db => db.CreateTable());

        Assert.Equal(1, scope.Scalar(IndexQuery));
    }

    [Fact]
    public void ApplyRetention_AddsTheIndexToAnOldDatabase()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        UseOldSchema(scope);
        Assert.Equal(0, scope.Scalar(IndexQuery));

        ApplicationDB.ApplyRetention();

        Assert.Equal(1, scope.Scalar(IndexQuery));
    }

    // ---- Purge ----

    [Fact]
    public void ApplyRetention_RemovesOnlyOrphanedProcesses()
    {
        using var scope = new ApplicationDbScope(retentionDays: 0);
        using var db = scope.Start(Day1);
        db.PushBatch(Adapter, [("chrome", 100, 10), ("teams", 5, 1)]);
        scope.Execute("INSERT INTO Process(Name) VALUES('orphan-1')");
        scope.Execute("INSERT INTO Process(Name) VALUES('orphan-2')");

        ApplicationDB.ApplyRetention();

        Assert.Equal(["chrome", "teams"], scope.ProcessNames());
        Assert.Equal(2, scope.Scalar("SELECT COUNT(*) FROM ProcessDate"));
        Assert.Equal((100L, 10L), scope.Usage(Day1, "chrome"));
    }

    [Fact]
    public void UpdateDatesInDB_WhenTodayIsAlreadySetUp_DoesNothingExtra()
    {
        var expiredDay = new DateTime(2020, 1, 1);
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        scope.Execute($"INSERT INTO Date(Year, Month, Day) VALUES({expiredDay.Year}, {expiredDay.Month}, {expiredDay.Day})");

        // A reconnect on the same day: no purge, so the expired row stays.
        db.UpdateDatesInDB();
        Assert.Equal(1, scope.DateRows(expiredDay));

        // The first call on a new day purges.
        ApplicationDB.Today = () => Day1.AddDays(1);
        db.UpdateDatesInDB();
        Assert.Equal(0, scope.DateRows(expiredDay));
    }

    [Fact]
    public void NewDay_WithNothingExpired_DoesNotSweepOrphanProcesses()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        db.PushBatch(Adapter, [("chrome", 100, 10)]);
        scope.Execute("INSERT INTO Process(Name) VALUES('orphan')");

        // Nothing is past retention on the new day, so the orphan scan is skipped.
        ApplicationDB.Today = () => Day1.AddDays(1);
        db.PushBatch(Adapter, [("chrome", 1, 1)]);

        Assert.Equal(["chrome", "orphan"], scope.ProcessNames());
    }

    [Fact]
    public void NewDay_WithExpiredHistory_RemovesTheOrphanProcessesOnce()
    {
        var oldDay = new DateTime(2026, 1, 1);
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(oldDay);
        db.PushBatch(Adapter, [("old.exe", 5, 1)]);
        scope.Execute("INSERT INTO Process(Name) VALUES('orphan')");

        ApplicationDB.Today = () => Day1;
        db.PushBatch(Adapter, [("new.exe", 100, 10)]);

        Assert.Equal(["new.exe"], scope.ProcessNames());
    }

    // ---- Helpers ----

    private const string IndexQuery =
        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_ProcessDate_AdapterID_DateID'";

    /// <summary>Gives the scope's file the schema of a database from before the index existed.</summary>
    private static void UseOldSchema(ApplicationDbScope scope)
    {
        using var connection = scope.OpenConnection();
        ApplicationDB.CreateSchema(connection);
        using var command = connection.CreateCommand();
        command.CommandText = "DROP INDEX IX_ProcessDate_AdapterID_DateID";
        command.ExecuteNonQuery();
    }

    private static long IndexCount(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = '{name}'";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void Stage(NetworkProcess netProc, params (string Name, long Received, long Sent)[] rows)
    {
        lock (netProc.PushToDBBuffer)
        {
            foreach (var (name, received, sent) in rows)
            {
                if (!netProc.PushToDBBuffer.TryGetValue(name, out var pending) || pending == null)
                {
                    pending = new MyProcess_Small(name, 0, 0);
                    netProc.PushToDBBuffer[name] = pending;
                }

                pending.CurrentDataRecv += received;
                pending.CurrentDataSend += sent;
            }
        }
    }

    private static (long Received, long Sent)? UsageFor(ApplicationDbScope scope, string adapter, string process)
    {
        using var connection = scope.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT pd.DataReceived, pd.DataSent FROM ProcessDate pd " +
            "JOIN Process p ON p.ID = pd.ProcessID JOIN Adapter a ON a.ID = pd.AdapterID " +
            "WHERE a.Name = @Adapter AND p.Name = @Process";
        command.Parameters.AddWithValue("@Adapter", adapter);
        command.Parameters.AddWithValue("@Process", process);
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0), reader.GetInt64(1)) : null;
    }

    private static List<string> UsageRows(ApplicationDbScope scope)
    {
        using var connection = scope.OpenConnection();
        return TestUsageDb.ReadUsageRows(connection);
    }
}
