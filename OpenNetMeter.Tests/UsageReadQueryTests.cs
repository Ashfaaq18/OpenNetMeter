using Microsoft.Data.Sqlite;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

/// <summary>
/// The Summary "usage from" card, History and the weekly trend filter ProcessDate by adapter and date range. They
/// were rewritten to use IX_ProcessDate_AdapterID_DateID; these tests compare them with the joined queries they
/// replaced (copied below) on a seeded database, and check the new ones really use the index.
/// </summary>
public class UsageReadQueryTests
{
    // The queries as they were before the index-friendly rewrite.
    internal const string OldTotalsSql =
        "SELECT SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
        "FROM ProcessDate pd " +
        "JOIN Adapter a ON a.ID = pd.AdapterID " +
        "JOIN Date d ON d.ID = pd.DateID " +
        "WHERE a.Name = @AdapterName " +
        "AND (d.Year * 10000 + d.Month * 100 + d.Day) BETWEEN @StartDate AND @EndDate";

    internal const string OldDailyTotalsSql =
        "SELECT d.Year, d.Month, d.Day, SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
        "FROM ProcessDate pd " +
        "JOIN Adapter a ON a.ID = pd.AdapterID " +
        "JOIN Date d ON d.ID = pd.DateID " +
        "WHERE a.Name = @AdapterName " +
        "AND (d.Year * 10000 + d.Month * 100 + d.Day) BETWEEN @StartDate AND @EndDate " +
        "GROUP BY d.Year, d.Month, d.Day";

    internal const string OldProcessTotalsSql =
        "SELECT p.Name, SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
        "FROM ProcessDate pd " +
        "JOIN Process p ON p.ID = pd.ProcessID " +
        "JOIN Adapter a ON a.ID = pd.AdapterID " +
        "JOIN Date d ON d.ID = pd.DateID " +
        "WHERE a.Name = @AdapterName " +
        "AND (d.Year * 10000 + d.Month * 100 + d.Day) BETWEEN @StartDate AND @EndDate " +
        "GROUP BY p.ID, p.Name " +
        "ORDER BY p.Name";

    public static IEnumerable<object[]> Cases() =>
    [
        ["WiFi", new DateTime(2025, 12, 31), new DateTime(2026, 2, 1)],
        ["WiFi", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2)],
        ["WiFi", new DateTime(2026, 1, 3), new DateTime(2026, 1, 31)],
        ["Ethernet", new DateTime(2026, 1, 2), new DateTime(2026, 1, 2)],
        ["Ethernet", new DateTime(2026, 1, 1), new DateTime(2026, 2, 28)],
        ["WiFi", new DateTime(2026, 3, 1), new DateTime(2026, 3, 31)],
        ["No such adapter", new DateTime(2025, 1, 1), new DateTime(2026, 12, 31)],
        ["WiFi", new DateTime(2026, 2, 1), new DateTime(2025, 12, 31)],
    ];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Totals_MatchTheJoinedQuery(string adapter, DateTime start, DateTime end)
    {
        using var db = SeededDb();

        var actual = UsageSinceTracker.ReadDbTotals(db.Path, adapter, start, end);

        // ReadDbTotals puts a reversed range in order before querying.
        var (from, to) = end < start ? (end, start) : (start, end);
        var expected = Rows(db.Connection, OldTotalsSql, adapter, from, to);
        var row = Assert.Single(expected);
        Assert.Equal($"{actual.download}|{actual.upload}", row.Replace("NULL", "0"));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DailyTotals_MatchTheJoinedQuery(string adapter, DateTime start, DateTime end)
    {
        using var db = SeededDb();

        var actual = WeeklyUsageTrendViewModel.ReadDailyTotals(db.Path, adapter, start, end)
            .Select(day => $"{day.Key / 10000}|{day.Key / 100 % 100}|{day.Key % 100}|{day.Value.download}|{day.Value.upload}")
            .Order()
            .ToList();

        var expected = Rows(db.Connection, OldDailyTotalsSql, adapter, start, end).Order().ToList();
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ProcessTotals_MatchTheJoinedQuery(string adapter, DateTime start, DateTime end)
    {
        using var db = SeededDb();

        var actual = Rows(db.Connection, HistoryViewModel.ProcessTotalsSql, adapter, start, end);

        // Same rows in the same (name) order.
        Assert.Equal(Rows(db.Connection, OldProcessTotalsSql, adapter, start, end), actual);
    }

    [Fact]
    public void SeededDatabase_ProducesDataForTheComparisonsAbove()
    {
        using var db = SeededDb();

        // Guards the tests above against comparing two empty results.
        var all = new DateTime(2025, 12, 31);
        var rows = Rows(db.Connection, HistoryViewModel.ProcessTotalsSql, "WiFi", all, new DateTime(2026, 2, 1));
        Assert.Equal(["chrome|11|22", "code|7|8", "teams|1100|2200"], rows);
        Assert.Equal(["50000000000|7"], Rows(db.Connection, UsageSinceTracker.TotalsSql, "Ethernet", new DateTime(2026, 1, 5), new DateTime(2026, 1, 5)));
    }

    [Theory]
    [InlineData(nameof(UsageSinceTracker.TotalsSql))]
    [InlineData(nameof(HistoryViewModel.ProcessTotalsSql))]
    [InlineData(nameof(WeeklyUsageTrendViewModel.DailyTotalsSql))]
    public void NewQueries_SeekProcessDateByAdapterAndDateTogether(string which)
    {
        using var db = SeededDb();
        var sql = which switch
        {
            nameof(UsageSinceTracker.TotalsSql) => UsageSinceTracker.TotalsSql,
            nameof(HistoryViewModel.ProcessTotalsSql) => HistoryViewModel.ProcessTotalsSql,
            _ => WeeklyUsageTrendViewModel.DailyTotalsSql,
        };

        var plan = QueryPlan(db.Connection, sql);

        // Both columns of the index are used, so only the rows in the date range are read, and never a table scan.
        Assert.Contains(plan, line => line.Contains("IX_ProcessDate_AdapterID_DateID (AdapterID=? AND DateID=?)"));
        Assert.DoesNotContain(plan, line => line.StartsWith("SCAN pd") || line.StartsWith("SCAN ProcessDate"));
    }

    [Fact]
    public void WithoutTheIndex_TheJoinedQueryScansProcessDate()
    {
        // The state of a database from before the index existed, which is why the index is added on startup.
        using var db = SeededDb();
        using (var command = db.Connection.CreateCommand())
        {
            command.CommandText = "DROP INDEX IX_ProcessDate_AdapterID_DateID";
            command.ExecuteNonQuery();
        }

        var plan = QueryPlan(db.Connection, OldTotalsSql);

        Assert.Contains(plan, line => line.StartsWith("SCAN pd"));
    }

    internal static TestUsageDb SeededDb()
    {
        var db = new TestUsageDb();
        db.AddUsage(new DateTime(2025, 12, 31), "WiFi", "chrome", 1, 2);
        db.AddUsage(new DateTime(2026, 1, 1), "WiFi", "chrome", 10, 20);
        db.AddUsage(new DateTime(2026, 1, 1), "WiFi", "teams", 100, 200);
        db.AddUsage(new DateTime(2026, 1, 2), "WiFi", "teams", 1000, 2000);
        db.AddUsage(new DateTime(2026, 1, 2), "Ethernet", "chrome", 5, 6);
        db.AddUsage(new DateTime(2026, 1, 5), "Ethernet", "steam", 50_000_000_000, 7);
        db.AddUsage(new DateTime(2026, 2, 1), "WiFi", "code", 7, 8);
        db.AddUsage(new DateTime(2026, 2, 1), "Ethernet", "code", 9, 10);
        return db;
    }

    internal static List<string> Rows(SqliteConnection connection, string sql, string adapter, DateTime start, DateTime end)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@AdapterName", adapter);
        command.Parameters.AddWithValue("@StartDate", UsageDatabase.ToDateInt(start));
        command.Parameters.AddWithValue("@EndDate", UsageDatabase.ToDateInt(end));

        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            rows.Add(string.Join('|', values.Select(v => Convert.IsDBNull(v) ? "NULL" : v.ToString())));
        }

        return rows;
    }

    private static List<string> QueryPlan(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        command.Parameters.AddWithValue("@AdapterName", "WiFi");
        command.Parameters.AddWithValue("@StartDate", 20260101);
        command.Parameters.AddWithValue("@EndDate", 20260131);

        using var reader = command.ExecuteReader();
        var plan = new List<string>();
        while (reader.Read())
            plan.Add(reader.GetString(3));

        return plan;
    }
}
