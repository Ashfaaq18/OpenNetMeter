using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenNetMeter.Models;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Services;

/// <summary>
/// Exports a finished (or running) traffic recording to CSV, JSON or a self-contained HTML report.
/// </summary>
internal static class RecordingExporter
{
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:sszzz";

    public static void WriteCsv(TextWriter writer, TrafficRecording recording)
    {
        writer.WriteLine("RecordingStart,RecordingEnd,DurationSeconds,Adapters,Process,DownloadBytes,UploadBytes");

        var start = UsageExporter.EscapeCsv(recording.StartedAt.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
        var end = UsageExporter.EscapeCsv(recording.StoppedAt?.ToString(DateTimeFormat, CultureInfo.InvariantCulture) ?? string.Empty);
        var duration = GetDurationSeconds(recording).ToString(CultureInfo.InvariantCulture);
        var adapters = UsageExporter.EscapeCsv(string.Join("; ", recording.Adapters));

        foreach (var process in recording.GetProcesses())
        {
            writer.Write(start);
            writer.Write(',');
            writer.Write(end);
            writer.Write(',');
            writer.Write(duration);
            writer.Write(',');
            writer.Write(adapters);
            writer.Write(',');
            writer.Write(UsageExporter.EscapeCsv(process.Process));
            writer.Write(',');
            writer.Write(process.DownloadBytes.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.WriteLine(process.UploadBytes.ToString(CultureInfo.InvariantCulture));
        }
    }

    public static void WriteJson(Stream stream, TrafficRecording recording)
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("startedAt", new DateTimeOffset(recording.StartedAt));
        if (recording.StoppedAt is { } stoppedAt)
            writer.WriteString("stoppedAt", new DateTimeOffset(stoppedAt));
        else
            writer.WriteNull("stoppedAt");
        writer.WriteNumber("durationSeconds", GetDurationSeconds(recording));

        writer.WriteStartArray("adapters");
        foreach (var adapter in recording.Adapters)
            writer.WriteStringValue(adapter);
        writer.WriteEndArray();

        writer.WriteNumber("totalDownloadBytes", recording.TotalDownload);
        writer.WriteNumber("totalUploadBytes", recording.TotalUpload);

        writer.WriteStartArray("processes");
        foreach (var process in recording.GetProcesses())
        {
            writer.WriteStartObject();
            writer.WriteString("process", process.Process);
            writer.WriteNumber("downloadBytes", process.DownloadBytes);
            writer.WriteNumber("uploadBytes", process.UploadBytes);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    internal static void WriteHtml(TextWriter writer, TrafficRecording recording)
    {
        HtmlReport.WriteDocumentStart(writer, "OpenNetMeter recording");
        HtmlReport.WriteMeta(writer,
        [
            ("Started", HtmlReport.FormatDateTime(recording.StartedAt)),
            ("Ended", recording.StoppedAt is { } stoppedAt ? HtmlReport.FormatDateTime(stoppedAt) : "Still running"),
            ("Duration", TrafficRecording.FormatElapsed(recording.GetDuration(DateTime.Now))),
            ("Adapters", recording.Adapters.Count > 0 ? string.Join(", ", recording.Adapters) : "None"),
            (HtmlReport.TotalDownloadLabel, ByteSizeFormatter.FormatBytes(recording.TotalDownload)),
            (HtmlReport.TotalUploadLabel, ByteSizeFormatter.FormatBytes(recording.TotalUpload))
        ]);

        HtmlReport.WriteProcessTable(writer, recording.GetProcesses()
            .Select(p => (p.Process, Download: p.DownloadBytes, Upload: p.UploadBytes)));

        HtmlReport.WriteDocumentEnd(writer);
    }

    /// <summary>
    /// Writes the recording to <paramref name="outputPath"/>; JSON if the extension is .json, an HTML report
    /// for .html or .htm, CSV otherwise.
    /// </summary>
    public static Task ExportAsync(TrafficRecording recording, string outputPath)
    {
        return Task.Run(() =>
        {
            UsageExporter.WriteFileAtomically(outputPath, stream =>
            {
                if (string.Equals(Path.GetExtension(outputPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    WriteJson(stream, recording);
                }
                else if (HtmlReport.IsHtmlPath(outputPath))
                {
                    // The page declares its charset, so no byte order mark is needed.
                    using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    WriteHtml(writer, recording);
                }
                else
                {
                    using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    WriteCsv(writer, recording);
                }
            });
        });
    }

    // GetDuration uses the stop time when there is one, so "now" only matters for a recording that is still running.
    private static long GetDurationSeconds(TrafficRecording recording) =>
        (long)recording.GetDuration(DateTime.Now).TotalSeconds;
}
