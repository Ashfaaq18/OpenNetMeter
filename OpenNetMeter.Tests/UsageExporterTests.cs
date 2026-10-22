using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenNetMeter.Services;

namespace OpenNetMeter.Tests;

public class UsageExporterTests
{
    [Fact]
    public void ReadRows_ReturnsEveryRowOrderedByDateAdapterProcess()
    {
        using var db = SeededDb();

        var rows = UsageExporter.ReadRows(db.Path);

        Assert.Equal(4, rows.Count);
        Assert.Equal(new UsageExportRow(new DateOnly(2026, 3, 1), "Ethernet", "chrome.exe", 100, 10), rows[0]);
        Assert.Equal(new UsageExportRow(new DateOnly(2026, 3, 1), "WiFi", "chrome.exe", 200, 20), rows[1]);
        Assert.Equal(new UsageExportRow(new DateOnly(2026, 3, 2), "WiFi", "steam.exe", 300, 30), rows[2]);
        Assert.Equal(new DateOnly(2026, 3, 10), rows[3].Date);
    }

    [Fact]
    public void ReadRows_FiltersByAdapterAndInclusiveDateRange()
    {
        using var db = SeededDb();

        var rows = UsageExporter.ReadRows(db.Path, "WiFi", new DateTime(2026, 3, 1), new DateTime(2026, 3, 2));

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("WiFi", r.Adapter));
        Assert.Equal(["chrome.exe", "steam.exe"], rows.Select(r => r.Process));
    }

    [Fact]
    public void ReadRows_ReturnsEmptyWhenDatabaseIsMissing()
    {
        var rows = UsageExporter.ReadRows(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.sqlite"));

        Assert.Empty(rows);
    }

    [Fact]
    public void WriteCsv_QuotesFieldsThatNeedEscaping()
    {
        var rows = new[]
        {
            new UsageExportRow(new DateOnly(2026, 3, 1), "Wi-Fi, 5GHz", "my \"app\".exe", 1234, 56)
        };
        var writer = new StringWriter { NewLine = "\n" };

        UsageExporter.WriteCsv(writer, rows);

        Assert.Equal(
            "Date,Adapter,Process,DownloadBytes,UploadBytes\n" +
            "2026-03-01,\"Wi-Fi, 5GHz\",\"my \"\"app\"\".exe\",1234,56\n",
            writer.ToString());
    }

    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-1", "'-1")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("Wi-Fi", "Wi-Fi")]
    public void WriteCsv_NeutralizesSpreadsheetFormulas(string adapter, string expectedField)
    {
        var rows = new[] { new UsageExportRow(new DateOnly(2026, 3, 1), adapter, "app.exe", 1, 2) };
        var writer = new StringWriter { NewLine = "\n" };

        UsageExporter.WriteCsv(writer, rows);

        Assert.Equal($"2026-03-01,{expectedField},app.exe,1,2", writer.ToString().Split('\n')[1]);
    }

    [Fact]
    public async Task ExportAsync_FailureLeavesExistingFileAndNoTempFile()
    {
        var output = Path.Combine(Path.GetTempPath(), $"onm-export-{Guid.NewGuid():N}.csv");
        File.WriteAllText(output, "previous export");
        try
        {
            // A zero-byte file is not a valid database, so reading fails before anything is written.
            var brokenDb = Path.Combine(Path.GetTempPath(), $"onm-broken-{Guid.NewGuid():N}.sqlite");
            File.WriteAllBytes(brokenDb, []);
            try
            {
                await Assert.ThrowsAnyAsync<Exception>(() => UsageExporter.ExportAsync(brokenDb, output));
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(brokenDb);
            }

            Assert.Equal("previous export", File.ReadAllText(output));
            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task ExportAsync_OverwritesExistingFile()
    {
        using var db = SeededDb();
        var output = Path.Combine(Path.GetTempPath(), $"onm-export-{Guid.NewGuid():N}.csv");
        File.WriteAllText(output, "previous export");
        try
        {
            await UsageExporter.ExportAsync(db.Path, output);

            Assert.StartsWith("Date,Adapter", File.ReadAllText(output).TrimStart('\uFEFF'));
            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Theory]
    [InlineData(@"C:\out\usage", "JSON file", @"C:\out\usage.json")]
    [InlineData(@"C:\out\usage", "CSV file", @"C:\out\usage.csv")]
    [InlineData(@"C:\out\usage", null, @"C:\out\usage.csv")]
    [InlineData(@"C:\out\usage.CSV", "JSON file", @"C:\out\usage.CSV")]
    [InlineData(@"C:\out\usage.json", "CSV file", @"C:\out\usage.json")]
    [InlineData(@"C:\out\usage.v2", "JSON file", @"C:\out\usage.v2.json")]
    [InlineData(@"C:\out\usage", "HTML report", @"C:\out\usage.html")]
    [InlineData(@"C:\out\usage.HTML", "CSV file", @"C:\out\usage.HTML")]
    public void ResolvePath_KeepsTypedExtensionOtherwiseUsesSelectedType(string path, string? selectedName, string expected)
    {
        var selected = UsageExporter.FileTypes.FirstOrDefault(t => t.Name == selectedName);

        Assert.Equal(expected, AvaloniaFileSaveDialogService.ResolvePath(path, selected, UsageExporter.FileTypes));
    }

    [Fact]
    public void WriteJson_ProducesRowsArray()
    {
        var rows = new[]
        {
            new UsageExportRow(new DateOnly(2026, 3, 1), "WiFi", "chrome.exe", 1234, 56)
        };
        using var stream = new MemoryStream();

        UsageExporter.WriteJson(stream, rows);

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        Assert.True(doc.RootElement.TryGetProperty("exportedAt", out _));
        var row = Assert.Single(doc.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal("2026-03-01", row.GetProperty("date").GetString());
        Assert.Equal("WiFi", row.GetProperty("adapter").GetString());
        Assert.Equal("chrome.exe", row.GetProperty("process").GetString());
        Assert.Equal(1234, row.GetProperty("downloadBytes").GetInt64());
        Assert.Equal(56, row.GetProperty("uploadBytes").GetInt64());
    }

    [Theory]
    [InlineData(".csv", "Date,Adapter")]
    [InlineData(".json", "{")]
    [InlineData(".JSON", "{")]
    public async Task ExportAsync_PicksFormatFromExtension(string extension, string expectedStart)
    {
        using var db = SeededDb();
        var output = Path.Combine(Path.GetTempPath(), $"onm-export-{Guid.NewGuid():N}{extension}");
        try
        {
            int count = await UsageExporter.ExportAsync(db.Path, output);

            Assert.Equal(4, count);
            var text = File.ReadAllText(output).TrimStart('﻿');
            Assert.StartsWith(expectedStart, text);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void FileTypes_ListsCsvFirstThenJsonThenHtml()
    {
        Assert.Equal(["csv", "json", "html"], UsageExporter.FileTypes.Select(t => t.Extension));
        Assert.Equal("HTML report", UsageExporter.FileTypes[2].Name);
    }

    [Theory]
    [InlineData(".html")]
    [InlineData(".htm")]
    [InlineData(".HTM")]
    public async Task ExportAsync_WritesHtmlReportWithoutByteOrderMarkOrTempFile(string extension)
    {
        using var db = SeededDb();
        var output = Path.Combine(Path.GetTempPath(), $"onm-export-{Guid.NewGuid():N}{extension}");
        try
        {
            int count = await UsageExporter.ExportAsync(db.Path, output);

            Assert.Equal(4, count);
            var bytes = File.ReadAllBytes(output);
            Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            var text = Encoding.UTF8.GetString(bytes);
            Assert.StartsWith("<!DOCTYPE html>", text);
            Assert.Contains("<h2>Ethernet</h2>", text);
            Assert.Contains("<h2>WiFi</h2>", text);
            Assert.False(File.Exists(output + ".tmp"));
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task ExportAsync_HtmlHonoursAdapterAndDateFilters()
    {
        using var db = SeededDb();
        var output = Path.Combine(Path.GetTempPath(), $"onm-export-{Guid.NewGuid():N}.html");
        try
        {
            int count = await UsageExporter.ExportAsync(db.Path, output, "WiFi", new DateTime(2026, 3, 1), new DateTime(2026, 3, 2));

            Assert.Equal(2, count);
            var text = File.ReadAllText(output);
            Assert.Contains("<dt>Profile</dt><dd>WiFi</dd>", text);
            Assert.DoesNotContain("Ethernet", text);
            Assert.Contains($"<dd>{HtmlTestHelpers.Day(2026, 3, 1)} {HtmlTestHelpers.EnDash} {HtmlTestHelpers.Day(2026, 3, 2)}</dd>", text);
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public async Task ExportAsync_CsvAndJsonAreUnchangedByTheHtmlOption()
    {
        using var db = SeededDb();
        var csv = Path.Combine(Path.GetTempPath(), $"onm-export-{Guid.NewGuid():N}.csv");
        var json = Path.Combine(Path.GetTempPath(), $"onm-export-{Guid.NewGuid():N}.json");
        try
        {
            await UsageExporter.ExportAsync(db.Path, csv);
            await UsageExporter.ExportAsync(db.Path, json);

            var csvText = File.ReadAllText(csv);
            Assert.DoesNotContain("<html", csvText);
            Assert.Equal(5, csvText.TrimEnd().Split('\n').Length); // header + four rows
            using var doc = JsonDocument.Parse(File.ReadAllBytes(json));
            Assert.Equal(4, doc.RootElement.GetProperty("rows").GetArrayLength());
            Assert.False(File.Exists(csv + ".tmp"));
            Assert.False(File.Exists(json + ".tmp"));
        }
        finally
        {
            File.Delete(csv);
            File.Delete(json);
        }
    }

    [Fact]
    public void WriteHtml_ShowsTotalsAndOneSectionPerAdapter()
    {
        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, HtmlRows(), null, null, null));

        Assert.Contains("<title>OpenNetMeter usage report</title>", html);
        Assert.Contains("<dt>Profiles</dt><dd>Ethernet, WiFi</dd>", html);
        Assert.Contains(
            $"<dt>Period</dt><dd>{HtmlTestHelpers.Day(2026, 3, 1)} {HtmlTestHelpers.EnDash} {HtmlTestHelpers.Day(2026, 3, 10)}</dd>",
            html);
        Assert.Contains("<dt>Total download</dt><dd class=\"dl\">1000 B</dd>", html);
        Assert.Contains("<dt>Total upload</dt><dd class=\"ul\">105 B</dd>", html);
        Assert.Equal(["Ethernet", "WiFi"], Regex.Matches(html, "<h2>(.*?)</h2>").Select(m => m.Groups[1].Value));
        Assert.Equal(2, Regex.Matches(html, "<h3>By process</h3>").Count);
        Assert.Equal(2, Regex.Matches(html, "<h3>By day</h3>").Count);
        Assert.Contains("Generated by OpenNetMeter on", html);
        HtmlTestHelpers.AssertNoActiveOrExternalContent(html);
    }

    [Fact]
    public void WriteHtml_ByProcessSumsTheWholePeriodLargestFirst()
    {
        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, HtmlRows(), null, null, null));

        var (byProcess, _) = SplitAdapterSection(html, "WiFi");

        // chrome.exe: 200 + 350 down, 20 + 40 up; steam.exe: 50 + 300 down, 5 + 30 up.
        Assert.Equal(["chrome.exe", "steam.exe"], HtmlTestHelpers.SummaryNames(byProcess));
        Assert.Contains("title=\"550 bytes\"", byProcess);
        Assert.Contains("title=\"60 bytes\"", byProcess);
        Assert.Contains("title=\"610 bytes\"", byProcess);
        Assert.Contains("title=\"350 bytes\"", byProcess);
        Assert.Contains("title=\"35 bytes\"", byProcess);
        Assert.Contains("title=\"385 bytes\"", byProcess);
        Assert.Equal(["100.0", "63.1"], HtmlTestHelpers.ShareWidths(byProcess));
    }

    [Fact]
    public void WriteHtml_EachProcessExpandsToItsOwnDaysAscending()
    {
        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, HtmlRows(), null, null, null));

        var (byProcess, _) = SplitAdapterSection(html, "WiFi");

        var blocks = HtmlTestHelpers.ProcessBlocks(byProcess);
        Assert.Equal(2, blocks.Count);
        Assert.DoesNotContain("<details open", byProcess);

        // chrome.exe ran on 1 and 10 March; the summary keeps the whole-period 610 bytes.
        Assert.Contains("title=\"610 bytes\"", blocks[0]);
        Assert.Equal([HtmlTestHelpers.Day(2026, 3, 1), HtmlTestHelpers.Day(2026, 3, 10)], HtmlTestHelpers.DayNames(blocks[0]));
        Assert.Contains("title=\"220 bytes\"", blocks[0]);
        Assert.Contains("title=\"390 bytes\"", blocks[0]);

        // steam.exe ran on 1 and 2 March only (50/5 and 300/30).
        Assert.Contains("title=\"385 bytes\"", blocks[1]);
        Assert.Equal([HtmlTestHelpers.Day(2026, 3, 1), HtmlTestHelpers.Day(2026, 3, 2)], HtmlTestHelpers.DayNames(blocks[1]));
        Assert.Contains("title=\"55 bytes\"", blocks[1]);
        Assert.Contains("title=\"330 bytes\"", blocks[1]);
    }

    [Fact]
    public void WriteHtml_ProcessDayRowsSkipDaysWithoutTrafficAndSumRowsOfTheSameDay()
    {
        var rows = new[]
        {
            new UsageExportRow(new DateOnly(2026, 3, 3), "WiFi", "app.exe", 50, 0),
            new UsageExportRow(new DateOnly(2026, 3, 1), "WiFi", "app.exe", 100, 10),
            new UsageExportRow(new DateOnly(2026, 3, 2), "WiFi", "app.exe", 0, 0),
            new UsageExportRow(new DateOnly(2026, 3, 1), "WiFi", "app.exe", 20, 1)
        };

        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, rows, null, null, null));

        var block = Assert.Single(HtmlTestHelpers.ProcessBlocks(html));
        Assert.Equal([HtmlTestHelpers.Day(2026, 3, 1), HtmlTestHelpers.Day(2026, 3, 3)], HtmlTestHelpers.DayNames(block));
        Assert.Contains("title=\"120 bytes\"", block); // 1 March download: 100 + 20
        Assert.Contains("title=\"11 bytes\"", block);  // 1 March upload: 10 + 1
        Assert.Contains("title=\"131 bytes\"", block); // 1 March total
    }

    [Fact]
    public void WriteHtml_ByDayListsOneAscendingRowPerDateWithDailySums()
    {
        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, HtmlRows(), null, null, null));

        var (_, byDay) = SplitAdapterSection(html, "WiFi");

        Assert.Equal(
            [HtmlTestHelpers.Day(2026, 3, 1), HtmlTestHelpers.Day(2026, 3, 2), HtmlTestHelpers.Day(2026, 3, 10)],
            HtmlTestHelpers.FirstCells(byDay));
        // 1 March adds chrome.exe and steam.exe together: 250 down, 25 up.
        Assert.Contains("title=\"250 bytes\"", byDay);
        Assert.Contains("title=\"25 bytes\"", byDay);
        Assert.Contains("title=\"275 bytes\"", byDay);
        Assert.Contains("title=\"330 bytes\"", byDay);
        Assert.Contains("title=\"390 bytes\"", byDay);
        Assert.DoesNotContain("chrome.exe", byDay);
        Assert.DoesNotContain("class=\"track\"", byDay);

        var (ethernetByProcess, ethernetByDay) = SplitAdapterSection(html, "Ethernet");
        Assert.Equal(["chrome.exe"], HtmlTestHelpers.SummaryNames(ethernetByProcess));
        Assert.Equal([HtmlTestHelpers.Day(2026, 3, 1)], HtmlTestHelpers.FirstCells(ethernetByDay));
    }

    [Fact]
    public void WriteHtml_FilteredReportHasOneSectionAndTheRequestedPeriod()
    {
        var wifi = HtmlRows().Where(r => r.Adapter == "WiFi").ToList();

        var html = HtmlTestHelpers.Render(w =>
            UsageExporter.WriteHtml(w, wifi, "WiFi", new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59)));

        Assert.Contains("<dt>Profile</dt><dd>WiFi</dd>", html);
        Assert.Contains(
            $"<dd>{HtmlTestHelpers.Day(2026, 3, 1)} {HtmlTestHelpers.EnDash} {HtmlTestHelpers.Day(2026, 3, 31)}</dd>",
            html);
        Assert.Equal(["WiFi"], Regex.Matches(html, "<h2>(.*?)</h2>").Select(m => m.Groups[1].Value));
    }

    [Fact]
    public void WriteHtml_SingleDayPeriodShowsOneDate()
    {
        var rows = new[] { new UsageExportRow(new DateOnly(2026, 3, 1), "WiFi", "app.exe", 1, 2) };

        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, rows, null, null, null));

        Assert.Contains($"<dt>Period</dt><dd>{HtmlTestHelpers.Day(2026, 3, 1)}</dd>", html);
    }

    [Fact]
    public void WriteHtml_WithoutRowsShowsMetaAndNoTrafficParagraph()
    {
        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, [], null, null, null));

        Assert.Contains("<dt>Profile</dt><dd>None</dd>", html);
        Assert.Contains("<dt>Period</dt><dd>No data</dd>", html);
        Assert.Contains("<dt>Total download</dt><dd class=\"dl\">0 B</dd>", html);
        Assert.Contains("<p class=\"empty\">No traffic recorded.</p>", html);
        Assert.DoesNotContain("<table", html);
        Assert.DoesNotContain("<h2>", html);
    }

    [Fact]
    public void WriteHtml_EmptyFilteredReportKeepsTheRequestedProfileAndPeriod()
    {
        var html = HtmlTestHelpers.Render(w =>
            UsageExporter.WriteHtml(w, [], "WiFi", new DateTime(2026, 3, 1), new DateTime(2026, 3, 5)));

        Assert.Contains("<dt>Profile</dt><dd>WiFi</dd>", html);
        Assert.Contains(
            $"<dd>{HtmlTestHelpers.Day(2026, 3, 1)} {HtmlTestHelpers.EnDash} {HtmlTestHelpers.Day(2026, 3, 5)}</dd>",
            html);
        Assert.Contains("No traffic recorded.", html);
    }

    [Fact]
    public void WriteHtml_EncodesHostileAdapterAndProcessNames()
    {
        const string scriptName = "<script>alert(1)</script>";
        const string handlerName = "\"><img src=x onerror=alert(1)>";
        var rows = new[]
        {
            new UsageExportRow(new DateOnly(2026, 3, 1), scriptName, handlerName, 10, 20),
            new UsageExportRow(new DateOnly(2026, 3, 1), handlerName, scriptName, 30, 40)
        };

        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, rows, null, null, null));

        // Both names are plain text, so the encoded form is all that remains of them.
        Assert.Contains("<h2>&lt;script&gt;alert(1)&lt;/script&gt;</h2>", html);
        Assert.Contains("<h2>&quot;&gt;&lt;img src=x onerror=alert(1)&gt;</h2>", html);
        Assert.Contains("<span class=\"pname\">&lt;script&gt;alert(1)&lt;/script&gt;</span>", html);
        Assert.Contains("<span class=\"pname\">&quot;&gt;&lt;img src=x onerror=alert(1)&gt;</span>", html);
        Assert.Contains("<dd>&quot;&gt;&lt;img src=x onerror=alert(1)&gt;, &lt;script&gt;alert(1)&lt;/script&gt;</dd>", html);
        HtmlTestHelpers.AssertNoActiveOrExternalContent(html);
    }

    [Fact]
    public void WriteHtml_EncodesTheProfileFilter()
    {
        var html = HtmlTestHelpers.Render(w => UsageExporter.WriteHtml(w, [], "<script>alert(1)</script>", null, null));

        Assert.Contains("<dd>&lt;script&gt;alert(1)&lt;/script&gt;</dd>", html);
        HtmlTestHelpers.AssertNoActiveOrExternalContent(html);
    }

    private static UsageExportRow[] HtmlRows() =>
    [
        new(new DateOnly(2026, 3, 1), "WiFi", "chrome.exe", 200, 20),
        new(new DateOnly(2026, 3, 1), "WiFi", "steam.exe", 50, 5),
        new(new DateOnly(2026, 3, 2), "WiFi", "steam.exe", 300, 30),
        new(new DateOnly(2026, 3, 10), "WiFi", "chrome.exe", 350, 40),
        new(new DateOnly(2026, 3, 1), "Ethernet", "chrome.exe", 100, 10)
    ];

    // Returns the by-process and by-day parts of one adapter's section.
    private static (string ByProcess, string ByDay) SplitAdapterSection(string html, string adapter)
    {
        int start = html.IndexOf($"<h2>{adapter}</h2>", StringComparison.Ordinal);
        Assert.True(start >= 0, $"No section for {adapter}");
        int end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        var section = html[start..end];

        int split = section.IndexOf("<h3>By day</h3>", StringComparison.Ordinal);
        return (section[..split], section[split..]);
    }

    private static TestUsageDb SeededDb()
    {
        var db = new TestUsageDb();
        db.AddUsage(new DateTime(2026, 3, 10), "WiFi", "chrome.exe", 400, 40);
        db.AddUsage(new DateTime(2026, 3, 2), "WiFi", "steam.exe", 300, 30);
        db.AddUsage(new DateTime(2026, 3, 1), "WiFi", "chrome.exe", 200, 20);
        db.AddUsage(new DateTime(2026, 3, 1), "Ethernet", "chrome.exe", 100, 10);
        return db;
    }
}
