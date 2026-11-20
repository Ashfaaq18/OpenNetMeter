using Avalonia;
using Avalonia.Media;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

/// <summary>
/// History is loaded lazily: the constructor must not touch the database, and refreshes requested
/// while the tab is hidden are deferred until it is shown. DeleteAllDbFiles closes ApplicationDB's shared
/// connection, so these run in the serial ApplicationDB collection.
/// </summary>
[Collection(ApplicationDbCollection.Name)]
public class HistoryViewModelTests
{
    private sealed class NoLinks : IExternalLinkService
    {
        public void Open(string uri) { }
    }

    private sealed class FakeImage : IImage
    {
        public Size Size => new(16, 16);
        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect) { }
    }

    private sealed class FakeIconService : IProcessIconService
    {
        public IImage Cached { get; } = new FakeImage();
        public IImage Loaded { get; } = new FakeImage();
        public List<string> Requested { get; } = [];

        public object? GetCachedIcon(string processName) => Cached;

        public Task<object?> LoadIconAsync(string processName)
        {
            Requested.Add(processName);
            return Task.FromResult<object?>(Loaded);
        }
    }

    private static HistoryViewModel Create(TestUsageDb db, IProcessIconService? icons = null) =>
        new(db.Path, icons ?? new PlaceholderProcessIconService(), new NoLinks(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService());

    [Fact]
    public void Constructor_DoesNotReadTheDatabase()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);

        var history = Create(db);

        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);
        Assert.Null(history.SelectedProfile);
        Assert.False(history.HasLastFiltered);
    }

    [Fact]
    public void Activate_FirstTimeLoadsProfilesSelectsTheFirstAndFilters()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);

        history.Activate();

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
        Assert.Equal("Ethernet", history.SelectedProfile);
        var row = Assert.Single(history.Rows);
        Assert.Equal("teams", row.ProcessName);
        Assert.Equal(300, history.TotalDownload);
        Assert.Equal(30, history.TotalUpload);
        Assert.True(history.HasLastFiltered);
        Assert.Equal("Ethernet", history.LastFilteredProfile);
    }

    [Fact]
    public void Activate_FirstTime_RefreshesTheDefaultDateRangeToToday()
    {
        using var db = new TestUsageDb();
        var history = Create(db);
        // Simulates the range the constructor computed days ago, before History was ever opened.
        history.DateStart = DateTimeOffset.Now.Date.AddDays(-20);
        history.DateEnd = DateTimeOffset.Now.Date.AddDays(-13);

        history.Activate();

        Assert.Equal(DateTimeOffset.Now.Date.AddDays(-7), history.DateStart);
        Assert.Equal(DateTimeOffset.Now.Date, history.DateEnd);
    }

    [Fact]
    public void Activate_FirstTime_SelectsThePreferredProfileWhenItHasHistory()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);
        history.PreferredProfile = "Wi-Fi";

        history.Activate();

        Assert.Equal("Wi-Fi", history.SelectedProfile);
        var row = Assert.Single(history.Rows);
        Assert.Equal("chrome", row.ProcessName);
        Assert.Equal("Wi-Fi", history.LastFilteredProfile);
    }

    [Fact]
    public void Activate_FirstTime_FallsBackToTheFirstProfileWhenThePreferredOneHasNoHistory()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);
        history.PreferredProfile = "Cellular";

        history.Activate();

        Assert.Equal("Ethernet", history.SelectedProfile);
    }

    [Fact]
    public void Activate_LaterActivationsKeepTheUsersSelectionRegardlessOfPreferredProfile()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);
        history.PreferredProfile = "Wi-Fi";
        history.Activate();
        history.SelectedProfile = "Ethernet";
        history.Deactivate();

        // A later connect (or a stale reload) must not yank the selection away from what the user chose.
        history.PreferredProfile = "Wi-Fi";
        history.ReloadProfiles();
        history.Activate();

        Assert.Equal("Ethernet", history.SelectedProfile);
    }

    [Fact]
    public void Activate_WithNoDatabaseLeavesHistoryEmpty()
    {
        using var db = new TestUsageDb();
        var missing = new HistoryViewModel(db.Path + ".missing", new PlaceholderProcessIconService(), new NoLinks(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService());

        missing.Activate();

        Assert.Empty(missing.Profiles);
        Assert.Empty(missing.Rows);
        Assert.False(missing.HasLastFiltered);
    }

    [Fact]
    public void ReloadProfiles_WhileHidden_OnlyMarksStaleUntilActivated()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        history.Deactivate();

        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        history.ReloadProfiles();

        Assert.Equal(["Wi-Fi"], history.Profiles);

        history.Activate();

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
        // The selection survives the reload, as it did when this ran on every network change.
        Assert.Equal("Wi-Fi", history.SelectedProfile);
    }

    [Fact]
    public void ReloadProfiles_BeforeFirstActivate_DoesNotLoadAnything()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);

        history.ReloadProfiles();

        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);
    }

    [Fact]
    public void ReloadProfiles_WhileActive_ReloadsImmediately()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();

        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        history.ReloadProfiles();

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
    }

    [Fact]
    public void Refresh_WhileHidden_DefersProfilesAndFilterToTheNextActivate()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        history.Deactivate();
        Assert.Equal(100, history.TotalDownload);

        db.AddUsage(DateTime.Today, "Wi-Fi", "firefox", 50, 5);
        history.Refresh();

        Assert.Single(history.Rows);
        Assert.Equal(100, history.TotalDownload);

        history.Activate();

        Assert.Equal(2, history.Rows.Count);
        Assert.Equal(150, history.TotalDownload);
    }

    [Fact]
    public void Activate_WithNothingStale_LeavesLoadedRowsAlone()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        var firstRow = history.Rows[0];
        history.Deactivate();

        // Not marked stale, so reopening the tab must not re-query (this row would be replaced by a new instance).
        db.AddUsage(DateTime.Today, "Wi-Fi", "firefox", 50, 5);
        history.Activate();

        Assert.Same(firstRow, Assert.Single(history.Rows));
    }

    [Fact]
    public void DeleteAllDbFiles_ClearsLoadedHistoryAndASubsequentReloadStaysEmpty()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        Assert.NotEmpty(history.Rows);
        db.Dispose();

        history.DeleteAllDbFiles();
        history.ReloadProfiles();

        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);
        Assert.Null(history.SelectedProfile);
        Assert.Equal(0, history.TotalDownload);
        Assert.False(history.HasLastFiltered);
    }

    [Fact]
    public void Filter_ReturnsTheSameRowsAsTheJoinedQueryItReplaced()
    {
        using var db = UsageReadQueryTests.SeededDb();
        var history = Create(db);
        history.Activate();
        var start = new DateTime(2025, 12, 31);
        var end = new DateTime(2026, 2, 1);

        history.SelectedProfile = "WiFi";
        history.DateStart = new DateTimeOffset(start);
        history.DateEnd = new DateTimeOffset(end);
        history.FilterCommand.Execute(null);

        var expected = UsageReadQueryTests.Rows(db.Connection, UsageReadQueryTests.OldProcessTotalsSql, "WiFi", start, end);
        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, history.Rows.Select(r => $"{r.ProcessName}|{r.DownloadBytes}|{r.UploadBytes}"));
        Assert.Equal(1 + 10 + 100 + 1000 + 7, history.TotalDownload);
        Assert.Equal(2 + 20 + 200 + 2000 + 8, history.TotalUpload);
    }

    [Fact]
    public void Rows_StartWithTheCachedIconAndRequestTheRealOne()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var icons = new FakeIconService();
        var history = Create(db, icons);

        history.Activate();

        Assert.Equal(["chrome"], icons.Requested);
        // The fake completes synchronously (as a cache hit does), so the loaded icon is already applied.
        Assert.Same(icons.Loaded, history.Rows[0].Icon);
    }

    [Fact]
    public void HistoryRow_RaisesPropertyChangedWhenTheIconChanges()
    {
        var row = new HistoryRowViewModel("chrome", 1, 2, new NoLinks());
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        var icon = new FakeImage();

        row.Icon = icon;
        row.Icon = icon;

        Assert.Same(icon, row.Icon);
        Assert.Equal([nameof(HistoryRowViewModel.Icon)], raised);
    }

    [Fact]
    public void SummaryRow_RaisesPropertyChangedWhenTheIconChanges()
    {
        var row = new SummaryProcessRowViewModel("chrome", new NoLinks());
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        var icon = new FakeImage();

        row.Icon = icon;
        row.Icon = icon;

        Assert.Same(icon, row.Icon);
        Assert.Equal([nameof(SummaryProcessRowViewModel.Icon)], raised);
    }
}
