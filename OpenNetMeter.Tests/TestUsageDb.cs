using Microsoft.Data.Sqlite;
using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

/// <summary>
/// A temporary on-disk SQLite database with the real usage schema.
/// </summary>
internal sealed class TestUsageDb : IDisposable
{
    public TestUsageDb()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"onm-test-{Guid.NewGuid():N}.sqlite");
        Connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path }.ToString());
        Connection.Open();
        Execute("PRAGMA foreign_keys=ON;");
        ApplicationDB.CreateSchema(Connection);
    }

    public string Path { get; }
    public SqliteConnection Connection { get; }

    public void AddUsage(DateTime date, string adapter, string process, long received, long sent)
    {
        Execute("INSERT OR IGNORE INTO Adapter(Name) VALUES(@A)", ("@A", adapter));
        Execute("INSERT OR IGNORE INTO Process(Name) VALUES(@P)", ("@P", process));
        Execute("INSERT OR IGNORE INTO Date(Year, Month, Day) VALUES(@Y, @M, @D)",
            ("@Y", date.Year), ("@M", date.Month), ("@D", date.Day));
        Execute(
            "INSERT INTO ProcessDate(ProcessID, DateID, AdapterID, DataReceived, DataSent) VALUES(" +
            "(SELECT ID FROM Process WHERE Name = @P), " +
            "(SELECT ID FROM Date WHERE Year = @Y AND Month = @M AND Day = @D), " +
            "(SELECT ID FROM Adapter WHERE Name = @A), @R, @S)",
            ("@P", process), ("@Y", date.Year), ("@M", date.Month), ("@D", date.Day),
            ("@A", adapter), ("@R", received), ("@S", sent));
    }

    public void AddDate(DateTime date) =>
        Execute("INSERT OR IGNORE INTO Date(Year, Month, Day) VALUES(@Y, @M, @D)",
            ("@Y", date.Year), ("@M", date.Month), ("@D", date.Day));

    /// <summary>
    /// What the per-row push ran for one process before flushes were batched, kept as the reference the batch
    /// is compared against. The Date row for <paramref name="day"/> must exist (see <see cref="AddDate"/>).
    /// </summary>
    public void OldPerRowPush(string adapter, DateTime day, string process, long received, long sent)
    {
        Execute("INSERT OR IGNORE INTO Process(Name) VALUES(@P)", ("@P", process));
        Execute("INSERT OR IGNORE INTO Adapter(Name) VALUES(@A)", ("@A", adapter));

        long processId = Scalar($"SELECT ID FROM Process WHERE Name = '{process}'");
        long adapterId = Scalar($"SELECT ID FROM Adapter WHERE Name = '{adapter}'");
        long dateId = Scalar($"SELECT ID FROM Date WHERE Year = {day.Year} AND Month = {day.Month} AND Day = {day.Day}");

        int inserted = Execute(
            "INSERT OR IGNORE INTO ProcessDate(ProcessID, DateID, AdapterID, DataReceived, DataSent) " +
            "VALUES(@P, @D, @A, @R, @S)",
            ("@P", processId), ("@D", dateId), ("@A", adapterId), ("@R", received), ("@S", sent));
        if (inserted < 1)
        {
            Execute(
                "UPDATE ProcessDate SET DataReceived = DataReceived + @R, DataSent = DataSent + @S " +
                "WHERE ProcessID = @P AND DateID = @D AND AdapterID = @A",
                ("@R", received), ("@S", sent), ("@P", processId), ("@D", dateId), ("@A", adapterId));
        }
    }

    /// <summary>Runs the statement a batched flush uses for one process, creating the rows it refers to first.</summary>
    public void Upsert(string process, DateTime day, string adapter, long received, long sent)
    {
        Execute("INSERT OR IGNORE INTO Process(Name) VALUES(@P)", ("@P", process));
        Execute("INSERT OR IGNORE INTO Adapter(Name) VALUES(@A)", ("@A", adapter));
        AddDate(day);

        Execute(
            ApplicationDB.UpsertProcessDateSql,
            ("@ProcessID", Scalar($"SELECT ID FROM Process WHERE Name = '{process}'")),
            ("@DateID", Scalar($"SELECT ID FROM Date WHERE Year = {day.Year} AND Month = {day.Month} AND Day = {day.Day}")),
            ("@AdapterID", Scalar($"SELECT ID FROM Adapter WHERE Name = '{adapter}'")),
            ("@DataReceived", received),
            ("@DataSent", sent));
    }

    /// <summary>Every usage row as "adapter|process|date|received|sent", sorted, without database ids.</summary>
    public List<string> UsageRows() => ReadUsageRows(Connection);

    public static List<string> ReadUsageRows(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT a.Name, p.Name, d.Year, d.Month, d.Day, pd.DataReceived, pd.DataSent FROM ProcessDate pd " +
            "JOIN Adapter a ON a.ID = pd.AdapterID JOIN Process p ON p.ID = pd.ProcessID JOIN Date d ON d.ID = pd.DateID";
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            rows.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetInt32(2):0000}-{reader.GetInt32(3):00}-{reader.GetInt32(4):00}" +
                     $"|{reader.GetInt64(5)}|{reader.GetInt64(6)}");
        }

        rows.Sort(StringComparer.Ordinal);
        return rows;
    }

    public long Scalar(string sql)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    public void Dispose()
    {
        Connection.Dispose();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            try { File.Delete(Path + suffix); } catch (IOException) { }
        }
    }

    private int Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command.ExecuteNonQuery();
    }
}
