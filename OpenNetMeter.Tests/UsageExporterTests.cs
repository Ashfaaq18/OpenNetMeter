using System.Text;
using System.Text.Json;
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
