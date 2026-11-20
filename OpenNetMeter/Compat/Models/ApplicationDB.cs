using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Models;

internal sealed class ApplicationDB : IDisposable
{
    private static readonly object DbLock = new();
    private static SqliteConnection? sharedConnection;
    private static int refCount;

    // The day whose Date row (and retention purge) has been taken care of. Static because instances come and go
    // (each ApplyRetention call, each capture session); guarded by DbLock; cleared when the shared connection is
    // closed with CloseSharedConnection.
    private static DateTime? ensuredDate;

    // Row ids PushBatch has already resolved, so a flush doesn't look them up again. Guarded by DbLock and cleared
    // whenever the connection closes or anything that could have deleted those rows runs (see ClearIdCachesLocked).
    // Ordinal: the UNIQUE(Name) columns compare case-sensitively, so "Chrome" and "chrome" are different rows.
    private static readonly Dictionary<string, long> adapterIds = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> processIds = new(StringComparer.Ordinal);
    private static (DateTime Day, long Id)? cachedDateId;

    /// <summary>Clock used for all date-dependent logic. Tests replace it to simulate midnight.</summary>
    internal static Func<DateTime> Today { get; set; } = () => DateTime.Today;

    /// <summary>Test seam: when set, the database lives at this path instead of the per-user app folder.</summary>
    internal static string? DatabasePathOverride { get; set; }

    private readonly string adapterName;
    private SqliteConnection? countedConnection;
    private bool disposed;

    public const string UnifiedDBFileName = "OpenNetMeter";

    private const string CreateAdapterTableSql =
        "CREATE TABLE IF NOT EXISTS Adapter(" +
        "ID INTEGER PRIMARY KEY NOT NULL, " +
        "Name TEXT NOT NULL UNIQUE)";

    private const string CreateProcessTableSql =
        "CREATE TABLE IF NOT EXISTS Process(" +
        "ID INTEGER PRIMARY KEY NOT NULL, " +
        "Name TEXT NOT NULL UNIQUE)";

    private const string CreateDateTableSql =
        "CREATE TABLE IF NOT EXISTS Date(" +
        "ID INTEGER PRIMARY KEY NOT NULL, " +
        "Year INTEGER NOT NULL, " +
        "Month INTEGER NOT NULL, " +
        "Day INTEGER NOT NULL, " +
        "UNIQUE (Year, Month, Day) ON CONFLICT IGNORE)";

    private const string CreateProcessDateTableSql =
        "CREATE TABLE IF NOT EXISTS ProcessDate(" +
        "ID INTEGER PRIMARY KEY NOT NULL, " +
        "ProcessID INTEGER NOT NULL, " +
        "DateID INTEGER NOT NULL, " +
        "AdapterID INTEGER NOT NULL, " +
        "DataReceived INTEGER NOT NULL, " +
        "DataSent INTEGER NOT NULL, " +
        "FOREIGN KEY(ProcessID) REFERENCES Process(ID) ON DELETE CASCADE, " +
        "FOREIGN KEY(DateID) REFERENCES Date(ID) ON DELETE CASCADE, " +
        "FOREIGN KEY(AdapterID) REFERENCES Adapter(ID) ON DELETE CASCADE, " +
        "UNIQUE (ProcessID, DateID, AdapterID) ON CONFLICT IGNORE)";

    // Without this, each Date row deleted by the retention purge cascades via a full ProcessDate scan.
    private const string CreateProcessDateDateIndexSql =
        "CREATE INDEX IF NOT EXISTS IX_ProcessDate_DateID ON ProcessDate(DateID)";

    // The history, summary and trend reads filter by adapter and date range; without this they scan ProcessDate.
    private const string CreateProcessDateAdapterDateIndexSql =
        "CREATE INDEX IF NOT EXISTS IX_ProcessDate_AdapterID_DateID ON ProcessDate(AdapterID, DateID)";

    /// <summary>
    /// Adds a flush's usage to the (process, day, adapter) row, creating it when missing. The table's
    /// UNIQUE(ProcessID, DateID, AdapterID) is declared ON CONFLICT IGNORE; naming the conflict target here makes
    /// this statement's DO UPDATE take precedence over that clause.
    /// </summary>
    internal const string UpsertProcessDateSql =
        "INSERT INTO ProcessDate(ProcessID, DateID, AdapterID, DataReceived, DataSent) " +
        "VALUES(@ProcessID, @DateID, @AdapterID, @DataReceived, @DataSent) " +
        "ON CONFLICT(ProcessID, DateID, AdapterID) DO UPDATE SET " +
        "DataReceived = DataReceived + excluded.DataReceived, " +
        "DataSent = DataSent + excluded.DataSent";

    public ApplicationDB(string dBFileName)
    {
        adapterName = dBFileName;

        lock (DbLock)
        {
            AcquireLocked();
        }
    }

    /// <summary>Whether the shared connection is open right now. Tests use it to check it outlives a flush.</summary>
    internal static bool IsConnectionOpen
    {
        get
        {
            lock (DbLock)
            {
                return sharedConnection != null;
            }
        }
    }

    public static string GetUnifiedDBFullPath()
    {
        return DatabasePathOverride ?? Path.Combine(Properties.Global.GetFilePath(), UnifiedDBFileName + ".sqlite");
    }

    public static void CloseSharedConnection()
    {
        lock (DbLock)
        {
            sharedConnection?.Dispose();
            sharedConnection = null;
            refCount = 0;
            // The database file may be deleted and recreated next, so today's Date row must be inserted again.
            ensuredDate = null;
            ClearIdCachesLocked();
        }
    }

    public int CreateTable()
    {
        lock (DbLock)
        {
            return CreateSchemaLocked();
        }
    }

    public int InsertUniqueRow_AdapterTable(string adapter)
    {
        lock (DbLock)
        {
            return RunNonQuery(
                "INSERT OR IGNORE INTO Adapter(Name) VALUES(@Name)",
                ("@Name", adapter));
        }
    }

    public void UpdateDatesInDB()
    {
        lock (DbLock)
        {
            // Today's Date row is in place and the purge already ran for today: nothing to do on a reconnect.
            var today = Today();
            if (ensuredDate == today)
                return;

            EnsureDate(today);
        }
    }

    /// <summary>
    /// Inserts <paramref name="day"/>'s Date row and purges expired history. Caller must hold DbLock.
    /// Must run before a push creates its Process row, or RemoveOldProcess would delete that fresh row.
    /// </summary>
    private void EnsureDate(DateTime day)
    {
        InsertUniqueRow_DateTable(day);
        // Process rows only become orphans when their last ProcessDate row goes, so the (scanning) orphan
        // cleanup is only worth running when the retention purge actually deleted something.
        if (RemoveOldDate() > 0)
            RemoveOldProcess();
        ensuredDate = day;
    }

    /// <summary>
    /// Deletes history older than the configured retention period. Safe to call while capture is running.
    /// </summary>
    public static void ApplyRetention()
    {
        if (!File.Exists(GetUnifiedDBFullPath()))
            return;

        // Hold the lock across create/purge/dispose so CloseSharedConnection (Delete All) can't interleave.
        lock (DbLock)
        {
            using var db = new ApplicationDB(string.Empty);
            CreateIndexes();
            db.RemoveOldDate();
            // Unconditional here: this is the explicit purge, so also sweep up orphans left by anything else.
            db.RemoveOldProcess();
        }
    }

    /// <summary>
    /// Creates all tables on the given connection. Lets tests build a database with the real schema.
    /// </summary>
    internal static void CreateSchema(SqliteConnection connection)
    {
        RunNonQuery(connection, CreateAdapterTableSql);
        RunNonQuery(connection, CreateProcessTableSql);
        RunNonQuery(connection, CreateDateTableSql);
        RunNonQuery(connection, CreateProcessDateTableSql);
        RunNonQuery(connection, CreateProcessDateDateIndexSql);
        RunNonQuery(connection, CreateProcessDateAdapterDateIndexSql);
    }

    /// <summary>
    /// Deletes Date rows older than <paramref name="cutoff"/>. ProcessDate rows follow via ON DELETE CASCADE,
    /// which requires PRAGMA foreign_keys=ON on the connection.
    /// </summary>
    internal static int DeleteDatesBefore(SqliteConnection connection, DateTime cutoff)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Date WHERE (Year * 10000 + Month * 100 + Day) < @Cutoff";
        command.Parameters.AddWithValue("@Cutoff", (cutoff.Year * 10000) + (cutoff.Month * 100) + cutoff.Day);
        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Adds one process's usage for today. Kept for callers that have a single row; flushes use
    /// <see cref="PushBatch"/> directly. Throws if the write fails.
    /// </summary>
    public void PushToDB(string processName, long totalDataRecv, long totalDataSend)
    {
        PushBatch(adapterName, [(processName, totalDataRecv, totalDataSend)]);
    }

    /// <summary>
    /// Adds a whole flush's usage for today to the database in one transaction: either every row is written or,
    /// if anything throws, none is (and the caller can retry the same batch without double counting).
    /// </summary>
    /// <param name="adapter">The adapter the usage was observed on; its row is created when missing.</param>
    /// <param name="batch">Bytes received and sent per process since the previous flush.</param>
    public void PushBatch(string adapter, IReadOnlyCollection<(string Name, long Received, long Sent)> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0)
            return;

        lock (DbLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            EnsureConnectedLocked();

            // A session that runs past midnight has no Date row for the new day until one is inserted here.
            var today = Today();
            if (ensuredDate != today)
                EnsureDate(today);

            long dateId = GetDateIdLocked(today);
            if (dateId < 1)
            {
                // Today was set up but its row is gone (the database was replaced underneath us): insert it again.
                InsertUniqueRow_DateTable(today);
                dateId = GetDateIdLocked(today);
            }

            if (dateId < 1)
            {
                // The Date insert failed; forget that today was set up so the next push retries it.
                ensuredDate = null;
                throw new InvalidOperationException(
                    $"SQLite push skipped: no Date row for {today:yyyy-MM-dd}; usage for this day cannot be saved yet");
            }

            var connection = Connection;
            try
            {
                // IMMEDIATE takes the write lock up front, so busy_timeout applies from the start of the batch.
                using var transaction = connection.BeginTransaction(deferred: false);

                long adapterId = ResolveId(adapterIds, "Adapter", adapter, connection, transaction);

                using var upsert = connection.CreateCommand();
                upsert.Transaction = transaction;
                upsert.CommandText = UpsertProcessDateSql;
                var processParameter = upsert.Parameters.Add("@ProcessID", SqliteType.Integer);
                var dateParameter = upsert.Parameters.Add("@DateID", SqliteType.Integer);
                var adapterParameter = upsert.Parameters.Add("@AdapterID", SqliteType.Integer);
                var receivedParameter = upsert.Parameters.Add("@DataReceived", SqliteType.Integer);
                var sentParameter = upsert.Parameters.Add("@DataSent", SqliteType.Integer);
                upsert.Prepare();

                dateParameter.Value = dateId;
                adapterParameter.Value = adapterId;
                foreach (var (name, received, sent) in batch)
                {
                    processParameter.Value = ResolveId(processIds, "Process", name, connection, transaction);
                    receivedParameter.Value = received;
                    sentParameter.Value = sent;
                    upsert.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                // Ids resolved inside the rolled-back transaction may belong to rows that were never committed.
                ClearIdCachesLocked();
                throw;
            }
        }
    }

    public List<List<object>> GetDataSum_ProcessDateTable(DateTime date1, DateTime date2)
    {
        lock (DbLock)
        {
            return GetMultipleCellData(
                "SELECT p1.Name, SUM(pd1.DataReceived), SUM(pd1.DataSent) " +
                "FROM ProcessDate pd1 " +
                "JOIN Process p1 ON p1.ID = pd1.ProcessID " +
                "WHERE DateID IN " +
                "(SELECT ID FROM Date WHERE " +
                "(Year * 10000 + Month * 100 + Day) " +
                "BETWEEN " +
                $"({date1.Year * 10000 + date1.Month * 100 + date1.Day}) " +
                "AND " +
                $"({date2.Year * 10000 + date2.Month * 100 + date2.Day})) " +
                "AND AdapterID = @AdapterID " +
                "GROUP BY ProcessID",
                ("@AdapterID", GetID_AdapterTable_Internal(adapterName).ToString()));
        }
    }

    public (long, long) GetDataSumBetweenDates(DateTime startDate, DateTime endDate)
    {
        DateTime fromDate = startDate.Date;
        DateTime toDate = endDate.Date;

        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        lock (DbLock)
        {
            var sum = GetMultipleCellData(
                "SELECT SUM(DataReceived), SUM(DataSent) FROM ProcessDate " +
                "WHERE DateID IN " +
                "(SELECT ID FROM Date " +
                "WHERE (Year * 10000 + Month * 100 + Day) " +
                "BETWEEN " +
                $"({fromDate.Year * 10000 + fromDate.Month * 100 + fromDate.Day}) " +
                "AND " +
                $"({toDate.Year * 10000 + toDate.Month * 100 + toDate.Day})) " +
                "AND AdapterID = @AdapterID",
                ("@AdapterID", GetID_AdapterTable_Internal(adapterName).ToString()));

            if (sum.Count == 1 && sum[0].Count == 2)
            {
                long download = Convert.IsDBNull(sum[0][0]) ? 0 : Convert.ToInt64(sum[0][0]);
                long upload = Convert.IsDBNull(sum[0][1]) ? 0 : Convert.ToInt64(sum[0][1]);
                return (download, upload);
            }
        }

        return (0, 0);
    }

    public (long, long) GetTodayDataSum_ProcessDateTable()
    {
        lock (DbLock)
        {
            var today = Today();
            var sum = GetMultipleCellData(
                "SELECT SUM(DataReceived), SUM(DataSent) FROM ProcessDate " +
                "WHERE DateID IN " +
                "(SELECT ID FROM Date " +
                "WHERE (Year * 10000 + Month * 100 + Day) = (@Year * 10000 + @Month * 100 + @Day)) " +
                "AND AdapterID = @AdapterID",
                ("@Year", today.Year.ToString()),
                ("@Month", today.Month.ToString()),
                ("@Day", today.Day.ToString()),
                ("@AdapterID", GetID_AdapterTable_Internal(adapterName).ToString()));

            if (sum.Count == 1 && sum[0].Count == 2 &&
                !Convert.IsDBNull(sum[0][0]) &&
                !Convert.IsDBNull(sum[0][1]))
            {
                return (Convert.ToInt64(sum[0][0]), Convert.ToInt64(sum[0][1]));
            }
        }

        return (0, 0);
    }

    public long GetID_AdapterTable(string adapter)
    {
        lock (DbLock)
        {
            return GetID_AdapterTable_Internal(adapter);
        }
    }

    public List<string> GetAllAdapters()
    {
        var adapters = new List<string>();

        lock (DbLock)
        {
            var rows = GetMultipleCellData("SELECT Name FROM Adapter ORDER BY Name");
            foreach (var row in rows)
            {
                if (row.Count > 0 && !Convert.IsDBNull(row[0]))
                {
                    adapters.Add(Convert.ToString(row[0])!);
                }
            }
        }

        return adapters;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        lock (DbLock)
        {
            // CloseSharedConnection may have replaced the connection this instance was counted
            // against; decrementing then would release someone else's reference.
            if (!ReferenceEquals(sharedConnection, countedConnection))
                return;

            refCount--;
            if (refCount == 0 && sharedConnection != null)
            {
                sharedConnection.Dispose();
                sharedConnection = null;
                ClearIdCachesLocked();
            }
        }
    }

    private static SqliteConnection Connection =>
        sharedConnection ?? throw new InvalidOperationException("Database not initialized");

    /// <summary>
    /// Counts this instance against the shared connection, opening it first when nobody has it open. Caller must hold DbLock.
    /// </summary>
    private void AcquireLocked()
    {
        sharedConnection ??= OpenSharedConnection();
        refCount++;
        countedConnection = sharedConnection;
    }

    private static SqliteConnection OpenSharedConnection()
    {
        var path = GetUnifiedDBFullPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        var connection = new SqliteConnection(builder.ToString());
        try
        {
            connection.Open();

            RunNonQuery(connection, "PRAGMA journal_mode=WAL;");
            RunNonQuery(connection, "PRAGMA busy_timeout=5000;");
            RunNonQuery(connection, "PRAGMA synchronous=NORMAL;");
            RunNonQuery(connection, "PRAGMA foreign_keys=ON;");
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }

    /// <summary>
    /// A long-lived instance (a capture session holds one) outlives CloseSharedConnection when Delete All runs.
    /// Counts it against a fresh connection and rebuilds the schema, since the file was probably deleted.
    /// Caller must hold DbLock.
    /// </summary>
    private void EnsureConnectedLocked()
    {
        if (sharedConnection != null && ReferenceEquals(sharedConnection, countedConnection))
            return;

        AcquireLocked();
        CreateSchemaLocked();
    }

    private int CreateSchemaLocked()
    {
        return CreateAdapterTable() >> 31 |
               CreateProcessTable() >> 31 |
               CreateDateTable() >> 31 |
               CreateProcessDateTable() >> 31 |
               CreateIndexes();
    }

    private static int CreateIndexes()
    {
        return RunNonQuery(CreateProcessDateDateIndexSql) >> 31 |
               RunNonQuery(CreateProcessDateAdapterDateIndexSql) >> 31;
    }

    /// <summary>
    /// Forgets the resolved ids. Caller must hold DbLock. Run whenever the connection closes (the file may be
    /// replaced) and whenever rows that may be cached are deleted or a transaction that created them rolls back.
    /// </summary>
    private static void ClearIdCachesLocked()
    {
        adapterIds.Clear();
        processIds.Clear();
        cachedDateId = null;
    }

    private long GetDateIdLocked(DateTime day)
    {
        if (cachedDateId is { } cached && cached.Day == day)
            return cached.Id;

        long id = GetID_DateTable(day);
        if (id > 0)
            cachedDateId = (day, id);

        return id;
    }

    /// <summary>
    /// The id of the <paramref name="table"/> row named <paramref name="name"/>, inserting the row when it is missing.
    /// Cached after the first lookup. <paramref name="table"/> is always a literal, never user input.
    /// </summary>
    private static long ResolveId(
        Dictionary<string, long> cache, string table, string name, SqliteConnection connection, SqliteTransaction transaction)
    {
        if (cache.TryGetValue(name, out long cached))
            return cached;

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT OR IGNORE INTO {table}(Name) VALUES(@Name)";
            insert.Parameters.AddWithValue("@Name", name);
            insert.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = $"SELECT ID FROM {table} WHERE Name = @Name";
        select.Parameters.AddWithValue("@Name", name);
        long id = Convert.ToInt64(select.ExecuteScalar()
            ?? throw new InvalidOperationException($"SQLite {table} row '{name}' is missing after insert"));

        cache[name] = id;
        return id;
    }

    private int CreateProcessTable()
    {
        return RunNonQuery(CreateProcessTableSql);
    }

    private int CreateDateTable()
    {
        return RunNonQuery(CreateDateTableSql);
    }

    private int CreateProcessDateTable()
    {
        return RunNonQuery(CreateProcessDateTableSql);
    }

    private int CreateAdapterTable()
    {
        return RunNonQuery(CreateAdapterTableSql);
    }

    private int InsertUniqueRow_DateTable(DateTime date)
    {
        return RunNonQuery(
            "INSERT OR IGNORE INTO Date(Year, Month, Day) VALUES(@Year, @Month, @Day)",
            ("@Year", date.Year.ToString()),
            ("@Month", date.Month.ToString()),
            ("@Day", date.Day.ToString()));
    }

    /// <returns>How many Date rows the retention purge deleted (0 when retention is forever or the purge failed).</returns>
    private int RemoveOldDate()
    {
        var cutoff = HistoryRetention.GetCutoff(Today(), Properties.SettingsManager.Current.HistoryRetentionDays);
        if (cutoff == null)
            return 0;

        try
        {
            int deleted = DeleteDatesBefore(Connection, cutoff.Value);
            if (deleted > 0)
                cachedDateId = null;

            return deleted;
        }
        catch (Exception ex)
        {
            EventLogger.Error("SQLite retention purge failed", ex);
            return 0;
        }
    }

    private void RemoveOldProcess()
    {
        int deleted = RunNonQuery(
            "DELETE FROM Process WHERE NOT EXISTS " +
            "(SELECT 1 FROM ProcessDate WHERE ProcessDate.ProcessID = Process.ID)");

        // A cached process id may belong to a row that was just deleted.
        if (deleted > 0)
            processIds.Clear();
    }

    private long GetID_DateTable(DateTime time)
    {
        var result = GetSingleCellData(
            "SELECT ID FROM Date WHERE Year = @Year AND Month = @Month AND Day = @Day",
            ("@Year", time.Year.ToString()),
            ("@Month", time.Month.ToString()),
            ("@Day", time.Day.ToString()));

        return Convert.ToInt64(result ?? -1);
    }

    private long GetID_AdapterTable_Internal(string adapter)
    {
        var result = GetSingleCellData(
            "SELECT ID FROM Adapter WHERE Name = @Name",
            ("@Name", adapter));

        return Convert.ToInt64(result ?? -1);
    }

    private static int RunNonQuery(string query, params (string Name, string Value)[] parameters)
    {
        var connection = sharedConnection;
        if (connection == null)
        {
            EventLogger.Error($"SQLite non-query skipped: database not initialized. Query='{query}'");
            return -1;
        }

        return RunNonQuery(connection, query, parameters);
    }

    private static int RunNonQuery(SqliteConnection connection, string query, params (string Name, string Value)[] parameters)
    {
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = query;
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            }

            return command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            EventLogger.Error($"SQLite non-query failed. Query='{query}'", ex);
            return -1;
        }
    }

    private static List<List<object>> GetMultipleCellData(string query, params (string Name, string Value)[] parameters)
    {
        var result = new List<List<object>>();

        try
        {
            using var command = Connection.CreateCommand();
            command.CommandText = query;
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            }

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var row = new List<object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.GetValue(i));
                }

                result.Add(row);
            }
        }
        catch (Exception ex)
        {
            EventLogger.Error($"SQLite multi-cell read failed. Query='{query}'", ex);
            return result;
        }

        return result;
    }

    private static object? GetSingleCellData(string query, params (string Name, string Value)[] parameters)
    {
        try
        {
            using var command = Connection.CreateCommand();
            command.CommandText = query;
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            }

            return command.ExecuteScalar();
        }
        catch (Exception ex)
        {
            EventLogger.Error($"SQLite single-cell read failed. Query='{query}'", ex);
            return null;
        }
    }

}
