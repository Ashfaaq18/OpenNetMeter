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

    private readonly string adapterName;
    private readonly SqliteConnection? countedConnection;
    private bool disposed;

    private static string LogTime() => DateTime.Now.ToString("HH:mm:ss.fff");

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

    public ApplicationDB(string dBFileName)
    {
        adapterName = dBFileName;

        lock (DbLock)
        {
            if (sharedConnection == null)
            {
                Directory.CreateDirectory(Properties.Global.GetFilePath());

                var builder = new SqliteConnectionStringBuilder
                {
                    DataSource = GetUnifiedDBFullPath(),
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared
                };

                sharedConnection = new SqliteConnection(builder.ToString());
                sharedConnection.Open();

                RunNonQuery(sharedConnection, "PRAGMA journal_mode=WAL;");
                RunNonQuery(sharedConnection, "PRAGMA busy_timeout=5000;");
                RunNonQuery(sharedConnection, "PRAGMA synchronous=NORMAL;");
                RunNonQuery(sharedConnection, "PRAGMA foreign_keys=ON;");
            }

            refCount++;
            countedConnection = sharedConnection;
        }

        Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] ApplicationDB created adapter='{dBFileName}'");
    }

    public static string GetUnifiedDBFullPath()
    {
        return Path.Combine(Properties.Global.GetFilePath(), UnifiedDBFileName + ".sqlite");
    }

    public static void CloseSharedConnection()
    {
        lock (DbLock)
        {
            sharedConnection?.Dispose();
            sharedConnection = null;
            refCount = 0;
        }
    }

    public int CreateTable()
    {
        lock (DbLock)
        {
            Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] CreateTable adapter='{adapterName}' db='{GetUnifiedDBFullPath()}'");
            return CreateAdapterTable() >> 31 |
                   CreateProcessTable() >> 31 |
                   CreateDateTable() >> 31 |
                   CreateProcessDateTable() >> 31 |
                   RunNonQuery(CreateProcessDateDateIndexSql) >> 31;
        }
    }

    public int InsertUniqueRow_AdapterTable(string adapter)
    {
        lock (DbLock)
        {
            Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] InsertUniqueRow_AdapterTable adapter='{adapter}'");
            return RunNonQuery(
                "INSERT OR IGNORE INTO Adapter(Name) VALUES(@Name)",
                ("@Name", adapter));
        }
    }

    public void UpdateDatesInDB()
    {
        lock (DbLock)
        {
            Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] UpdateDatesInDB adapter='{adapterName}' today='{DateTime.Today:yyyy-MM-dd}' retentionDays={Properties.SettingsManager.Current.HistoryRetentionDays}");
            InsertUniqueRow_DateTable(DateTime.Today);
            RemoveOldDate();
            RemoveOldProcess();
        }
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
            RunNonQuery(CreateProcessDateDateIndexSql);
            db.RemoveOldDate();
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

    public void PushToDB(string processName, long totalDataRecv, long totalDataSend)
    {
        lock (DbLock)
        {
            Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] PushToDB adapter='{adapterName}' process='{processName}' recv={totalDataRecv} sent={totalDataSend}");

            InsertUniqueRow_ProcessTable(processName);
            InsertUniqueRow_AdapterTable(adapterName);

            long dateID = GetID_DateTable(DateTime.Today);
            long processID = GetID_ProcessTable(processName);
            long adapterID = GetID_AdapterTable_Internal(adapterName);

            if (InsertUniqueRow_ProcessDateTable(processID, dateID, adapterID, totalDataRecv, totalDataSend) < 1)
            {
                UpdateRow_ProcessDateTable(processID, dateID, adapterID, totalDataRecv, totalDataSend);
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
            var sum = GetMultipleCellData(
                "SELECT SUM(DataReceived), SUM(DataSent) FROM ProcessDate " +
                "WHERE DateID IN " +
                "(SELECT ID FROM Date " +
                "WHERE (Year * 10000 + Month * 100 + Day) = (@Year * 10000 + @Month * 100 + @Day)) " +
                "AND AdapterID = @AdapterID",
                ("@Year", DateTime.Today.Year.ToString()),
                ("@Month", DateTime.Today.Month.ToString()),
                ("@Day", DateTime.Today.Day.ToString()),
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
            }
        }
    }

    private static SqliteConnection Connection =>
        sharedConnection ?? throw new InvalidOperationException("Database not initialized");

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

    private int InsertUniqueRow_ProcessTable(string appName)
    {
        return RunNonQuery(
            "INSERT OR IGNORE INTO Process(Name) VALUES(@Name)",
            ("@Name", appName));
    }

    private int InsertUniqueRow_DateTable(DateTime date)
    {
        return RunNonQuery(
            "INSERT OR IGNORE INTO Date(Year, Month, Day) VALUES(@Year, @Month, @Day)",
            ("@Year", date.Year.ToString()),
            ("@Month", date.Month.ToString()),
            ("@Day", date.Day.ToString()));
    }

    private void RemoveOldDate()
    {
        var cutoff = HistoryRetention.GetCutoff(DateTime.Today, Properties.SettingsManager.Current.HistoryRetentionDays);
        if (cutoff == null)
        {
            Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] RemoveOldDate skipped (retention=forever)");
            return;
        }

        Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] RemoveOldDate cutoff='{cutoff:yyyy-MM-dd}'");
        try
        {
            DeleteDatesBefore(Connection, cutoff.Value);
        }
        catch (Exception ex)
        {
            EventLogger.Error("SQLite retention purge failed", ex);
        }
    }

    private void RemoveOldProcess()
    {
        Console.WriteLine($"[{LogTime()}] [SQLite][Avalonia] RemoveOldProcess");
        RunNonQuery(
            "DELETE FROM Process WHERE ID NOT IN " +
            "(SELECT DISTINCT ProcessID FROM ProcessDate)");
    }

    private int InsertUniqueRow_ProcessDateTable(long processID, long dateID, long adapterID, long dataReceived, long dataSent)
    {
        return RunNonQuery(
            "INSERT OR IGNORE INTO ProcessDate(ProcessID, DateID, AdapterID, DataReceived, DataSent) " +
            "VALUES(@ProcessID, @DateID, @AdapterID, @DataReceived, @DataSent)",
            ("@ProcessID", processID.ToString()),
            ("@DateID", dateID.ToString()),
            ("@AdapterID", adapterID.ToString()),
            ("@DataReceived", dataReceived.ToString()),
            ("@DataSent", dataSent.ToString()));
    }

    private int UpdateRow_ProcessDateTable(long processID, long dateID, long adapterID, long dataReceived, long dataSent)
    {
        return RunNonQuery(
            "UPDATE ProcessDate SET " +
            "DataReceived = DataReceived + @DataReceived, " +
            "DataSent = DataSent + @DataSent " +
            "WHERE ProcessID = @ProcessID AND DateID = @DateID AND AdapterID = @AdapterID",
            ("@DataReceived", dataReceived.ToString()),
            ("@DataSent", dataSent.ToString()),
            ("@ProcessID", processID.ToString()),
            ("@DateID", dateID.ToString()),
            ("@AdapterID", adapterID.ToString()));
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

    private long GetID_ProcessTable(string appName)
    {
        var result = GetSingleCellData(
            "SELECT ID FROM Process WHERE Name = @Name",
            ("@Name", appName));

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
