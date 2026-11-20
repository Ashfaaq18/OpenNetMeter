using Microsoft.Data.Sqlite;
using OpenNetMeter.Models;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Tests;

/// <summary>
/// Points ApplicationDB at a temporary database and restores every piece of static state on dispose.
/// Only for tests in <see cref="ApplicationDbCollection"/>, since it replaces ApplicationDB's statics.
/// </summary>
internal sealed class ApplicationDbScope : IDisposable
{
    public const string Adapter = "WiFi";

    private readonly Func<DateTime> originalToday = ApplicationDB.Today;
    private readonly string? originalPathOverride = ApplicationDB.DatabasePathOverride;
    private readonly int originalRetentionDays = SettingsManager.Current.HistoryRetentionDays;

    public ApplicationDbScope(int retentionDays)
    {
        // Also clears the "today is set up" marker and id caches left by another test.
        ApplicationDB.CloseSharedConnection();
        ApplicationDB.DatabasePathOverride = Path;
        SettingsManager.Current.HistoryRetentionDays = retentionDays;
    }

    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"onm-test-{Guid.NewGuid():N}.sqlite");

    /// <summary>Runs <paramref name="work"/> on a fresh ApplicationDB, like one flush used to, with the clock at <paramref name="day"/>.</summary>
    public void AtDay(DateTime day, Action<ApplicationDB> work)
    {
        using var db = Open(day);
        work(db);
    }

    /// <summary>A new ApplicationDB (the caller disposes it) with the clock at <paramref name="day"/>.</summary>
    public ApplicationDB Open(DateTime day)
    {
        ApplicationDB.Today = () => day;
        return new ApplicationDB(Adapter);
    }

    /// <summary>Creates the tables and today's rows, like StartNetworkProcess does.</summary>
    public ApplicationDB Start(DateTime day)
    {
        var db = Open(day);
        db.CreateTable();
        db.InsertUniqueRow_AdapterTable(Adapter);
        db.UpdateDatesInDB();
        return db;
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
        var usage = TryUsage(day, process);
        Assert.True(usage.HasValue, $"no usage row for '{process}' on {day:yyyy-MM-dd}");
        return usage.Value;
    }

    public (long Received, long Sent)? TryUsage(DateTime day, string process)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT pd.DataReceived, pd.DataSent FROM ProcessDate pd " +
            "JOIN Process p ON p.ID = pd.ProcessID " +
            "JOIN Date d ON d.ID = pd.DateID " +
            $"WHERE p.Name = '{process}' AND d.Year = {day.Year} AND d.Month = {day.Month} AND d.Day = {day.Day}";
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0), reader.GetInt64(1)) : null;
    }

    public List<string> ProcessNames()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Process ORDER BY Name";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
            names.Add(reader.GetString(0));
        return names;
    }

    /// <summary>Removes the database files, like Delete All does after closing the shared connection.</summary>
    public void DeleteFiles()
    {
        ApplicationDB.CloseSharedConnection();
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            try { File.Delete(Path + suffix); } catch (IOException) { }
        }
    }

    public void Dispose()
    {
        ApplicationDB.CloseSharedConnection();
        ApplicationDB.Today = originalToday;
        ApplicationDB.DatabasePathOverride = originalPathOverride;
        SettingsManager.Current.HistoryRetentionDays = originalRetentionDays;

        DeleteFiles();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path }.ToString());
        connection.Open();
        return connection;
    }
}
