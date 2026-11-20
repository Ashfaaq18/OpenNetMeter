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
    public void Rebase(string adapterName, DateTime fromDate, long sessionDownload, long sessionUpload) =>
        Rebase(fromDate, sessionDownload, sessionUpload, ReadTotals(adapterName, fromDate));

    /// <summary>
    /// Reads the persisted totals from <paramref name="fromDate"/> to today, to be applied by the <c>Rebase</c> overload that takes them.
    /// Touches no tracker state, so it can run on a background thread while the UI thread keeps advancing the total.
    /// </summary>
    public (long download, long upload) ReadTotals(string adapterName, DateTime fromDate) =>
        string.IsNullOrWhiteSpace(adapterName)
            ? (0, 0)
            : readTotals(adapterName, fromDate.Date, DateTime.Today);

    /// <summary>
    /// Applies totals returned by <see cref="ReadTotals"/> (nothing is read here). The session counters must be the ones from the moment that
    /// read began: the total is then the persisted usage plus what the session counted since, however long the read took.
    /// </summary>
    public void Rebase(DateTime fromDate, long sessionDownload, long sessionUpload, (long download, long upload) dbTotals)
    {
        FromDate = fromDate.Date;
        sessionDownloadBaseline = sessionDownload;
        sessionUploadBaseline = sessionUpload;
        (dbDownloadBaseline, dbUploadBaseline) = dbTotals;

        Advance(sessionDownload, sessionUpload);
    }

    /// <summary>Moves the total forward to the current session counters.</summary>
    public void Advance(long sessionDownload, long sessionUpload)
    {
        Download = dbDownloadBaseline + Math.Max(0, sessionDownload - sessionDownloadBaseline);
        Upload = dbUploadBaseline + Math.Max(0, sessionUpload - sessionUploadBaseline);
    }

    /// <summary>
    /// Folds the session's contribution into the baseline so the total holds while session counters
    /// restart at 0, e.g. on disconnect.
    /// </summary>
    public void FreezeSession()
    {
        dbDownloadBaseline = Download;
        dbUploadBaseline = Upload;
        sessionDownloadBaseline = 0;
        sessionUploadBaseline = 0;
    }

    // Filters ProcessDate by adapter id and date ids so it can use IX_ProcessDate_AdapterID_DateID instead of
    // joining (and scanning) every row. Adapter.Name is unique, so the subquery yields at most one id.
    internal const string TotalsSql =
        "SELECT SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
        "FROM ProcessDate pd " +
        "WHERE pd.AdapterID = (SELECT ID FROM Adapter WHERE Name = @AdapterName) " +
        "AND pd.DateID IN (SELECT ID FROM Date " +
        "WHERE (Year * 10000 + Month * 100 + Day) BETWEEN @StartDate AND @EndDate)";

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
            command.CommandText = TotalsSql;
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
