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

    private void Execute(string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }
}
