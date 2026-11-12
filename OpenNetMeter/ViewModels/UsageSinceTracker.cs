using System;
using System.IO;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

/// <summary>
/// Live "usage since a date" total for one adapter: the persisted usage from <see cref="FromDate"/>
/// through today, snapshotted at <see cref="Rebase"/>, plus whatever the current session has observed
/// since that snapshot. Used by the Summary page's "Usage From" card and, independently, by the mini widget.
/// </summary>
internal sealed class UsageSinceTracker
{
    private readonly Func<string, DateTime, DateTime, (long download, long upload)> readTotals;
    private long dbDownloadBaseline;
    private long dbUploadBaseline;
    private long sessionDownloadBaseline;
    private long sessionUploadBaseline;

    public UsageSinceTracker()
        : this((adapter, from, to) => ReadDbTotals(UsageDatabase.ResolveDatabasePath(), adapter, from, to))
    {
    }

    internal UsageSinceTracker(Func<string, DateTime, DateTime, (long download, long upload)> readTotals)
    {
        this.readTotals = readTotals;
    }

    public DateTime FromDate { get; private set; } = DateTime.Today;
    public long Download { get; private set; }
    public long Upload { get; private set; }

    /// <summary>
    /// Re-reads the persisted totals from <paramref name="fromDate"/> to today and restarts counting
    /// session traffic from the given session counters.
    /// </summary>
    public void Rebase(string adapterName, DateTime fromDate, long sessionDownload, long sessionUpload)
    {
        FromDate = fromDate.Date;
        sessionDownloadBaseline = sessionDownload;
        sessionUploadBaseline = sessionUpload;

        if (string.IsNullOrWhiteSpace(adapterName))
            (dbDownloadBaseline, dbUploadBaseline) = (0, 0);
        else
            (dbDownloadBaseline, dbUploadBaseline) = readTotals(adapterName, FromDate, DateTime.Today);

        Advance(sessionDownload, sessionUpload);
    }

    /// <summary>Moves the total forward to the current session counters.</summary>
    public void Advance(long sessionDownload, long sessionUpload)
    {
        Download = dbDownloadBaseline + Math.Max(0, sessionDownload - sessionDownloadBaseline);
        Upload = dbUploadBaseline + Math.Max(0, sessionUpload - sessionUploadBaseline);
    }

    /// <summary>Zeroes the total (e.g. on disconnect), keeping <see cref="FromDate"/>.</summary>
    public void Reset()
    {
        dbDownloadBaseline = 0;
        dbUploadBaseline = 0;
        sessionDownloadBaseline = 0;
        sessionUploadBaseline = 0;
        Download = 0;
        Upload = 0;
    }

    internal static (long download, long upload) ReadDbTotals(string dbPath, string adapterName, DateTime startDate, DateTime endDate)
    {
        try
        {
            if (!File.Exists(dbPath))
                return (0, 0);

            var fromDate = startDate.Date;
            var toDate = endDate.Date;
            if (toDate < fromDate)
                (fromDate, toDate) = (toDate, fromDate);

            using var connection = UsageDatabase.OpenReadOnlyConnection(dbPath);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
                "FROM ProcessDate pd " +
                "JOIN Adapter a ON a.ID = pd.AdapterID " +
                "JOIN Date d ON d.ID = pd.DateID " +
                "WHERE a.Name = @AdapterName " +
                "AND (d.Year * 10000 + d.Month * 100 + d.Day) BETWEEN @StartDate AND @EndDate";
            command.Parameters.AddWithValue("@AdapterName", adapterName);
            command.Parameters.AddWithValue("@StartDate", UsageDatabase.ToDateInt(fromDate));
            command.Parameters.AddWithValue("@EndDate", UsageDatabase.ToDateInt(toDate));

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var download = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                var upload = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                return (download, upload);
            }
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to read usage totals from database for adapter '{adapterName}'", ex);
        }

        return (0, 0);
    }
}
