using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenNetMeter.Models;
using OpenNetMeter.Services;

namespace OpenNetMeter.Tests;

public class RecordingExporterTests
{
    // Local kind so the exported UTC offset is the machine's, whatever time zone the tests run in.
    private static readonly DateTime Start = new(2026, 3, 1, 14, 5, 9, DateTimeKind.Local);
    private static readonly DateTime Stop = Start.AddSeconds(151);

    [Fact]
    public void WriteCsv_WritesHeaderAndOneRowPerProcess()
    {
        var recording = StoppedRecording();
        var writer = new StringWriter { NewLine = "\n" };

        RecordingExporter.WriteCsv(writer, recording);

        var lines = writer.ToString().Split('\n');
        Assert.Equal("RecordingStart,RecordingEnd,DurationSeconds,Adapters,Process,DownloadBytes,UploadBytes", lines[0]);
        Assert.Equal(4, lines.Length); // header + two rows + the empty piece after the final newline
        Assert.Equal("", lines[3]);

        var offset = @"[+-]\d{2}:\d{2}";
        Assert.Matches($"^2026-03-01T14:05:09{offset},2026-03-01T14:07:40{offset},151,WiFi; Ethernet,chrome\\.exe,1500,200$", lines[1]);
        Assert.Matches($"^2026-03-01T14:05:09{offset},2026-03-01T14:07:40{offset},151,WiFi; Ethernet,steam\\.exe,100,50$", lines[2]);
    }

    [Fact]
    public void WriteCsv_QuotesCommasAndNeutralizesFormulas()
    {
        var recording = new TrafficRecording(Start);
        recording.NoteAdapter("Cafe, 5GHz");
        recording.Add("=evil", 1, 2);
        recording.Add("my \"app\".exe", 3, 4);
        recording.Stop(Stop);
        var writer = new StringWriter { NewLine = "\n" };

        RecordingExporter.WriteCsv(writer, recording);

        var lines = writer.ToString().Split('\n');
        // Rows follow GetProcesses order: the quoted name has more traffic (7 bytes vs 3), so it comes first.
        Assert.EndsWith(",151,\"Cafe, 5GHz\",\"my \"\"app\"\".exe\",3,4", lines[1]);
        Assert.EndsWith(",151,\"Cafe, 5GHz\",'=evil,1,2", lines[2]);
    }

    [Fact]
    public void WriteCsv_WithoutProcessesWritesOnlyTheHeader()
    {
        var recording = new TrafficRecording(Start);
        recording.Stop(Stop);
        var writer = new StringWriter { NewLine = "\n" };

        RecordingExporter.WriteCsv(writer, recording);

        Assert.Equal("RecordingStart,RecordingEnd,DurationSeconds,Adapters,Process,DownloadBytes,UploadBytes\n", writer.ToString());
    }

    [Fact]
    public void WriteJson_ProducesDocumentedFields()
    {
        var recording = StoppedRecording();
        using var stream = new MemoryStream();

        RecordingExporter.WriteJson(stream, recording);

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        var root = doc.RootElement;
        Assert.Equal(new DateTimeOffset(Start), root.GetProperty("startedAt").GetDateTimeOffset());
        Assert.Equal(new DateTimeOffset(Stop), root.GetProperty("stoppedAt").GetDateTimeOffset());
        Assert.Equal(151, root.GetProperty("durationSeconds").GetInt64());
        Assert.Equal(["WiFi", "Ethernet"], root.GetProperty("adapters").EnumerateArray().Select(a => a.GetString()!));
        Assert.Equal(1600, root.GetProperty("totalDownloadBytes").GetInt64());
        Assert.Equal(250, root.GetProperty("totalUploadBytes").GetInt64());

        var processes = root.GetProperty("processes").EnumerateArray().ToList();
        Assert.Equal(2, processes.Count);
        Assert.Equal("chrome.exe", processes[0].GetProperty("process").GetString());
        Assert.Equal(1500, processes[0].GetProperty("downloadBytes").GetInt64());
        Assert.Equal(200, processes[0].GetProperty("uploadBytes").GetInt64());
        Assert.Equal("steam.exe", processes[1].GetProperty("process").GetString());
        Assert.Equal(100, processes[1].GetProperty("downloadBytes").GetInt64());
        Assert.Equal(50, processes[1].GetProperty("uploadBytes").GetInt64());
    }

    [Fact]
    public void WriteJson_RunningRecordingHasNullStoppedAt()
    {
        var recording = new TrafficRecording(DateTime.Now.AddSeconds(-5));
        using var stream = new MemoryStream();

        RecordingExporter.WriteJson(stream, recording);

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("stoppedAt").ValueKind);
        Assert.True(doc.RootElement.GetProperty("durationSeconds").GetInt64() >= 5);
        Assert.Empty(doc.RootElement.GetProperty("adapters").EnumerateArray());
        Assert.Empty(doc.RootElement.GetProperty("processes").EnumerateArray());
    }

    [Theory]
    [InlineData(".csv", false)]
    [InlineData(".txt", false)]
    [InlineData(".json", true)]
    [InlineData(".JSON", true)]
    public async Task ExportAsync_PicksFormatFromExtensionAndLeavesNoTempFile(string extension, bool expectJson)
    {
        var output = Path.Combine(Path.GetTempPath(), $"onm-recording-{Guid.NewGuid():N}{extension}");
        try
        {
            await RecordingExporter.ExportAsync(StoppedRecording(), output);

            var bytes = File.ReadAllBytes(output);
            if (expectJson)
            {
                using var doc = JsonDocument.Parse(bytes);
                Assert.Equal(1600, doc.RootElement.GetProperty("totalDownloadBytes").GetInt64());
            }
            else
            {
                // CSV is written with a UTF-8 byte order mark so Excel reads it as UTF-8.
                Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
                Assert.StartsWith("RecordingStart,", Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
            }

            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    [InlineData(".HTM")]
    public async Task ExportAsync_WritesHtmlReportWithoutByteOrderMarkOrTempFile(string extension)
    {
        var output = Path.Combine(Path.GetTempPath(), $"onm-recording-{Guid.NewGuid():N}{extension}");
        try
        {
            await RecordingExporter.ExportAsync(StoppedRecording(), output);

            var bytes = File.ReadAllBytes(output);
            Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            var text = Encoding.UTF8.GetString(bytes);
            Assert.StartsWith("<!DOCTYPE html>", text);
            Assert.Contains("<title>OpenNetMeter recording</title>", text);
            Assert.Contains("<td>chrome.exe</td>", text);
            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void WriteHtml_StoppedRecordingShowsMetaAndProcessRows()
    {
        var recording = new TrafficRecording(Start);
        recording.NoteAdapter("WiFi");
        recording.NoteAdapter("Ethernet");
        recording.Add("steam.exe", 1024, 1024);
        recording.Add("chrome.exe", 2048, 1024);
        recording.Stop(Stop);

        var html = HtmlTestHelpers.Render(w => RecordingExporter.WriteHtml(w, recording));

        Assert.Contains("<dt>Started</dt><dd>" + HtmlReport.Encode(HtmlReport.FormatDateTime(Start)) + "</dd>", html);
        Assert.Contains("<dt>Ended</dt><dd>" + HtmlReport.Encode(HtmlReport.FormatDateTime(Stop)) + "</dd>", html);
        Assert.Contains("<dt>Duration</dt><dd>02:31</dd>", html);
        Assert.Contains("<dt>Adapters</dt><dd>WiFi, Ethernet</dd>", html);
        Assert.Contains("<dt>Total download</dt><dd class=\"dl\">3 KB</dd>", html);
        Assert.Contains("<dt>Total upload</dt><dd class=\"ul\">2 KB</dd>", html);
        Assert.DoesNotContain("Still running", html);

        Assert.Equal(["chrome.exe", "steam.exe"], HtmlTestHelpers.FirstCells(html));
        Assert.Contains("title=\"3,072 bytes\"", html); // chrome.exe total
        Assert.Contains("title=\"2,048 bytes\"", html); // steam.exe total
        Assert.Equal(["100.0", "66.7"], HtmlTestHelpers.ShareWidths(html));
        Assert.Contains("Generated by OpenNetMeter on", html);
        HtmlTestHelpers.AssertNoActiveOrExternalContent(html);
    }

    [Fact]
    public void WriteHtml_RunningRecordingShowsStillRunning()
    {
        var recording = new TrafficRecording(DateTime.Now.AddSeconds(-5));

        var html = HtmlTestHelpers.Render(w => RecordingExporter.WriteHtml(w, recording));

        Assert.Contains("<dt>Ended</dt><dd>Still running</dd>", html);
        Assert.Matches(@"<dt>Duration</dt><dd>00:0\d</dd>", html);
        Assert.Contains("<dt>Adapters</dt><dd>None</dd>", html);
        Assert.Contains("<p class=\"empty\">No traffic recorded.</p>", html);
        Assert.DoesNotContain("<table", html);
    }

    [Fact]
    public void WriteHtml_EncodesHostileAdapterAndProcessNames()
    {
        var recording = new TrafficRecording(Start);
        recording.NoteAdapter("<script>alert(1)</script>");
        recording.Add("\"><img src=x onerror=alert(1)>", 1, 2);
        recording.Stop(Stop);

        var html = HtmlTestHelpers.Render(w => RecordingExporter.WriteHtml(w, recording));

        Assert.Contains("<dt>Adapters</dt><dd>&lt;script&gt;alert(1)&lt;/script&gt;</dd>", html);
        Assert.Contains("<td>&quot;&gt;&lt;img src=x onerror=alert(1)&gt;</td>", html);
        HtmlTestHelpers.AssertNoActiveOrExternalContent(html);
    }

    [Fact]
    public async Task ExportAsync_OverwritesExistingFile()
    {
        var output = Path.Combine(Path.GetTempPath(), $"onm-recording-{Guid.NewGuid():N}.csv");
        File.WriteAllText(output, "previous export");
        try
        {
            await RecordingExporter.ExportAsync(StoppedRecording(), output);

            Assert.StartsWith("RecordingStart,", File.ReadAllText(output).TrimStart('﻿'));
            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void WriteFileAtomically_FailureLeavesExistingFileAndNoTempFile()
    {
        var output = Path.Combine(Path.GetTempPath(), $"onm-recording-{Guid.NewGuid():N}.csv");
        File.WriteAllText(output, "previous export");
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                UsageExporter.WriteFileAtomically(output, stream =>
                {
                    stream.WriteByte(1);
                    throw new InvalidOperationException("boom");
                }));

            Assert.Equal("previous export", File.ReadAllText(output));
            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    private static TrafficRecording StoppedRecording()
    {
        var recording = new TrafficRecording(Start);
        recording.NoteAdapter("WiFi");
        recording.NoteAdapter("Ethernet");
        recording.Add("steam.exe", 100, 50);
        recording.Add("chrome.exe", 1000, 100);
        recording.Add("chrome.exe", 500, 100);
        recording.Stop(Stop);
        return recording;
    }
}
