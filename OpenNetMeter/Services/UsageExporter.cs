using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Services;

internal sealed record UsageExportRow(DateOnly Date, string Adapter, string Process, long DownloadBytes, long UploadBytes);

/// <summary>
/// Exports per-day, per-process usage rows from the usage database to CSV, JSON or a self-contained HTML report.
/// </summary>
internal static class UsageExporter
{
    public static readonly IReadOnlyList<FileTypeOption> FileTypes =
    [
        new FileTypeOption("CSV file", "csv"),
        new FileTypeOption("JSON file", "json"),
        new FileTypeOption("HTML report", "html")
    ];

    /// <summary>
    /// Reads the stored rows, optionally filtered by adapter and an inclusive date range.
    /// </summary>
    public static List<UsageExportRow> ReadRows(string dbPath, string? adapter = null, DateTime? start = null, DateTime? end = null)
    {
        var rows = new List<UsageExportRow>();
        if (!File.Exists(dbPath))
            return rows;

        using var connection = UsageDatabase.OpenReadOnlyConnection(dbPath);
        connection.Open();

        using var command = connection.CreateCommand();
        var sql = new StringBuilder(
            "SELECT d.Year, d.Month, d.Day, a.Name, p.Name, pd.DataReceived, pd.DataSent " +
            "FROM ProcessDate pd " +
            "JOIN Process p ON p.ID = pd.ProcessID " +
            "JOIN Adapter a ON a.ID = pd.AdapterID " +
            "JOIN Date d ON d.ID = pd.DateID " +
            "WHERE 1 = 1");

        if (adapter != null)
        {
            sql.Append(" AND a.Name = @AdapterName");
            command.Parameters.AddWithValue("@AdapterName", adapter);
        }

        if (start != null)
        {
            sql.Append(" AND (d.Year * 10000 + d.Month * 100 + d.Day) >= @StartDate");
            command.Parameters.AddWithValue("@StartDate", UsageDatabase.ToDateInt(start.Value));
        }

        if (end != null)
        {
            sql.Append(" AND (d.Year * 10000 + d.Month * 100 + d.Day) <= @EndDate");
            command.Parameters.AddWithValue("@EndDate", UsageDatabase.ToDateInt(end.Value));
        }

        sql.Append(" ORDER BY d.Year, d.Month, d.Day, a.Name, p.Name");
        command.CommandText = sql.ToString();

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new UsageExportRow(
                new DateOnly(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                reader.IsDBNull(6) ? 0 : reader.GetInt64(6)));
        }

        return rows;
    }

    public static void WriteCsv(TextWriter writer, IEnumerable<UsageExportRow> rows)
    {
        writer.WriteLine("Date,Adapter,Process,DownloadBytes,UploadBytes");
        foreach (var row in rows)
        {
            writer.Write(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(EscapeCsv(row.Adapter));
            writer.Write(',');
            writer.Write(EscapeCsv(row.Process));
            writer.Write(',');
            writer.Write(row.DownloadBytes.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.WriteLine(row.UploadBytes.ToString(CultureInfo.InvariantCulture));
        }
    }

    public static void WriteJson(Stream stream, IEnumerable<UsageExportRow> rows)
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("exportedAt", DateTimeOffset.Now);
        writer.WriteStartArray("rows");
        foreach (var row in rows)
        {
            writer.WriteStartObject();
            writer.WriteString("date", row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.WriteString("adapter", row.Adapter);
            writer.WriteString("process", row.Process);
            writer.WriteNumber("downloadBytes", row.DownloadBytes);
            writer.WriteNumber("uploadBytes", row.UploadBytes);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes a summary page: totals, then per adapter a by-process table (each process expands to its days) and a
    /// by-day table. The filter, start and end only describe the report header.
    /// </summary>
    internal static void WriteHtml(TextWriter writer, IReadOnlyList<UsageExportRow> rows, string? adapterFilter, DateTime? start, DateTime? end)
    {
        var byAdapter = rows
            .GroupBy(r => r.Adapter)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = adapterFilter != null ? [adapterFilter] : byAdapter.Select(g => g.Key).ToList();
        long totalDownload = rows.Sum(r => r.DownloadBytes);
        long totalUpload = rows.Sum(r => r.UploadBytes);

        HtmlReport.WriteDocumentStart(writer, "OpenNetMeter usage report");
        HtmlReport.WriteMeta(writer,
        [
            (profiles.Count > 1 ? "Profiles" : "Profile", profiles.Count > 0 ? string.Join(", ", profiles) : "None"),
            ("Period", FormatHtmlPeriod(rows, start, end)),
            (HtmlReport.TotalDownloadLabel, ByteSizeFormatter.FormatBytes(totalDownload)),
            (HtmlReport.TotalUploadLabel, ByteSizeFormatter.FormatBytes(totalUpload))
        ]);

        if (rows.Count == 0)
            HtmlReport.WriteNoTraffic(writer);

        foreach (var adapter in byAdapter)
        {
            writer.WriteLine("<section>");
            writer.WriteLine($"<h2>{HtmlReport.Encode(adapter.Key)}</h2>");

            writer.WriteLine("<h3>By process</h3>");
            HtmlReport.WriteExpandableProcessTable(writer, SummarizeProcesses(adapter));

            writer.WriteLine("<h3>By day</h3>");
            HtmlReport.WriteDayTable(writer, adapter
                .GroupBy(r => r.Date)
                .Select(g => (Date: g.Key, Download: g.Sum(r => r.DownloadBytes), Upload: g.Sum(r => r.UploadBytes))));

            writer.WriteLine("</section>");
        }

        HtmlReport.WriteDocumentEnd(writer);
    }

    // One entry per process with its whole-period totals and its per-day totals, for the expandable by-process table.
    private static List<(string Process, long Download, long Upload, IReadOnlyList<(DateOnly Date, long Download, long Upload)> Days)> SummarizeProcesses(
        IEnumerable<UsageExportRow> adapterRows)
    {
        return adapterRows
            .GroupBy(r => r.Process)
            .Select(g => (
                Process: g.Key,
                Download: g.Sum(r => r.DownloadBytes),
                Upload: g.Sum(r => r.UploadBytes),
                Days: (IReadOnlyList<(DateOnly Date, long Download, long Upload)>)g
                    .GroupBy(r => r.Date)
                    .Select(d => (Date: d.Key, Download: d.Sum(r => r.DownloadBytes), Upload: d.Sum(r => r.UploadBytes)))
                    .ToList()))
            .ToList();
    }

    // The requested range wins; an open end falls back to the first or last day that has data.
    private static string FormatHtmlPeriod(IReadOnlyList<UsageExportRow> rows, DateTime? start, DateTime? end)
    {
        DateOnly? first = start != null ? DateOnly.FromDateTime(start.Value) : null;
        DateOnly? last = end != null ? DateOnly.FromDateTime(end.Value) : null;
        if (rows.Count > 0)
        {
            first ??= rows.Min(r => r.Date);
            last ??= rows.Max(r => r.Date);
        }

        if (first == null || last == null)
            return "No data";

        return first == last
            ? HtmlReport.FormatDate(first.Value)
            : HtmlReport.FormatDate(first.Value) + " \u2013 " + HtmlReport.FormatDate(last.Value);
    }

    /// <summary>
    /// Writes the matching rows to <paramref name="outputPath"/>; JSON if the extension is .json, an HTML report
    /// for .html or .htm, CSV otherwise. Returns the number of rows written.
    /// </summary>
    public static Task<int> ExportAsync(string dbPath, string outputPath, string? adapter = null, DateTime? start = null, DateTime? end = null)
    {
        return Task.Run(() =>
        {
            var rows = ReadRows(dbPath, adapter, start, end);

            WriteFileAtomically(outputPath, stream =>
            {
                if (string.Equals(Path.GetExtension(outputPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(stream, rows);
                }
                else if (HtmlReport.IsHtmlPath(outputPath))
                {
                    // The page declares its charset, so no byte order mark is needed.
                    using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    WriteHtml(writer, rows, adapter, start, end);
                }
                else
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    WriteCsv(writer, rows);
                }
            });

            return rows.Count;
        });
    }

    /// <summary>
    /// Writes through a temp file and moves it into place, so a failed export never truncates an existing file.
    /// </summary>
    internal static void WriteFileAtomically(string outputPath, Action<Stream> write)
    {
        string tempPath = outputPath + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                write(stream);
            }

            File.Move(tempPath, outputPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(tempPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    internal static string EscapeCsv(string value)
    {
        // Adapter names are Wi-Fi SSIDs, which anyone nearby can choose; stop spreadsheets
        // from evaluating them as formulas (OWASP CSV injection guidance).
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;

        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
