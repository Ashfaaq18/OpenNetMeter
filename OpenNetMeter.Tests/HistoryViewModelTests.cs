using Avalonia;
using Avalonia.Media;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

/// <summary>
/// History is loaded lazily: the constructor must not touch the database, and refreshes requested
/// while the tab is hidden are deferred until it is shown. The reads run on a background task and are applied
/// by a post to the UI thread; most tests post inline, the ones about ordering run the posts by hand.
/// DeleteAllDbFiles closes ApplicationDB's shared connection, so these run in the serial ApplicationDB collection.
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

    /// <summary>A stand-in for the UI thread: posts queue up until the test runs them.</summary>
    private sealed class UiQueue
    {
        private readonly List<Action> posted = [];

        public int Count
        {
            get { lock (posted) return posted.Count; }
        }

        public void Post(Action action)
        {
            lock (posted)
                posted.Add(action);
        }

        public void Run(int index)
        {
            Action action;
            lock (posted)
                action = posted[index];
            action();
        }

        public void RunAll()
        {
            for (var i = 0; i < Count; i++)
                Run(i);
        }
    }

    private static HistoryViewModel Create(TestUsageDb db, IProcessIconService? icons = null, Action<Action>? postToUi = null) =>
        new(db.Path, icons ?? new PlaceholderProcessIconService(), new NoLinks(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService(), postToUi ?? (action => action()));

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
    public async Task Activate_FirstTimeLoadsProfilesSelectsTheFirstAndFilters()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);

        history.Activate();
        await history.PendingLoad;

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
    public async Task Activate_ReadsInTheBackgroundAndAppliesOnTheUiPost()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);

        history.Activate();

        // Nothing is read or shown on the calling thread.
        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);

        await history.PendingLoad;

        // The read is done, but the result only reaches the view model when the UI thread runs the post.
        Assert.Equal(1, ui.Count);
        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);
        Assert.False(history.HasLastFiltered);

        ui.RunAll();

        Assert.Equal(["Wi-Fi"], history.Profiles);
        Assert.Equal("Wi-Fi", history.SelectedProfile);
        Assert.Single(history.Rows);
        Assert.Equal(100, history.TotalDownload);
        Assert.Equal("Wi-Fi", history.LastFilteredProfile);
    }

    [Fact]
    public async Task Activate_FirstTime_RefreshesTheDefaultDateRangeToToday()
    {
        using var db = new TestUsageDb();
        var history = Create(db);
        // Simulates the range the constructor computed days ago, before History was ever opened.
        history.DateStart = DateTimeOffset.Now.Date.AddDays(-20);
        history.DateEnd = DateTimeOffset.Now.Date.AddDays(-13);

        history.Activate();
        await history.PendingLoad;

        Assert.Equal(DateTimeOffset.Now.Date.AddDays(-7), history.DateStart);
        Assert.Equal(DateTimeOffset.Now.Date, history.DateEnd);
    }

    [Fact]
    public async Task Activate_FirstTime_SelectsThePreferredProfileWhenItHasHistory()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);
        history.PreferredProfile = "Wi-Fi";

        history.Activate();
        await history.PendingLoad;

        Assert.Equal("Wi-Fi", history.SelectedProfile);
        var row = Assert.Single(history.Rows);
        Assert.Equal("chrome", row.ProcessName);
        Assert.Equal("Wi-Fi", history.LastFilteredProfile);
    }

    [Fact]
    public async Task Activate_FirstTime_FallsBackToTheFirstProfileWhenThePreferredOneHasNoHistory()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);
        history.PreferredProfile = "Cellular";

        history.Activate();
        await history.PendingLoad;

        Assert.Equal("Ethernet", history.SelectedProfile);
    }

    [Fact]
    public async Task Activate_LaterActivationsKeepTheUsersSelectionRegardlessOfPreferredProfile()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var history = Create(db);
        history.PreferredProfile = "Wi-Fi";
        history.Activate();
        await history.PendingLoad;
        history.SelectedProfile = "Ethernet";
        history.Deactivate();

        // A later connect (or a stale reload) must not yank the selection away from what the user chose.
        history.PreferredProfile = "Wi-Fi";
        history.ReloadProfiles();
        history.Activate();
        await history.PendingLoad;

        Assert.Equal("Ethernet", history.SelectedProfile);
    }

    [Fact]
    public async Task Activate_WithNoDatabaseLeavesHistoryEmpty()
    {
        using var db = new TestUsageDb();
        var missing = new HistoryViewModel(db.Path + ".missing", new PlaceholderProcessIconService(), new NoLinks(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService(), action => action());

        missing.Activate();
        await missing.PendingLoad;

        Assert.Empty(missing.Profiles);
        Assert.Empty(missing.Rows);
        Assert.False(missing.HasLastFiltered);
    }

    [Fact]
    public async Task ReloadProfiles_WhileHidden_OnlyMarksStaleUntilActivated()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        await history.PendingLoad;
        history.Deactivate();

        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        history.ReloadProfiles();
        await history.PendingLoad;

        Assert.Equal(["Wi-Fi"], history.Profiles);

        history.Activate();
        await history.PendingLoad;

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
        // The selection survives the reload, as it did when this ran on every network change.
        Assert.Equal("Wi-Fi", history.SelectedProfile);
    }

    [Fact]
    public async Task ReloadProfiles_BeforeFirstActivate_DoesNotLoadAnything()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);

        history.ReloadProfiles();
        await history.PendingLoad;

        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);
    }

    [Fact]
    public async Task ReloadProfiles_WhileActive_ReloadsImmediately()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        await history.PendingLoad;

        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        history.ReloadProfiles();
        await history.PendingLoad;

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
    }

    [Fact]
    public async Task ReloadProfiles_KeepsTheChoiceTheUserMadeWhileItWasLoading()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);
        history.PreferredProfile = "Wi-Fi";
        history.Activate();
        await history.PendingLoad;
        ui.RunAll();
        Assert.Equal("Wi-Fi", history.SelectedProfile);

        history.ReloadProfiles();
        // The request has captured "Wi-Fi"; the user picks another profile before the result arrives.
        history.SelectedProfile = "Ethernet";
        await history.PendingLoad;
        ui.Run(1);

        Assert.Equal("Ethernet", history.SelectedProfile);
        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
    }

    [Fact]
    public async Task Refresh_WhileHidden_DefersProfilesAndFilterToTheNextActivate()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        await history.PendingLoad;
        history.Deactivate();
        Assert.Equal(100, history.TotalDownload);

        db.AddUsage(DateTime.Today, "Wi-Fi", "firefox", 50, 5);
        history.Refresh();
        await history.PendingLoad;

        Assert.Single(history.Rows);
        Assert.Equal(100, history.TotalDownload);

        history.Activate();
        await history.PendingLoad;

        Assert.Equal(2, history.Rows.Count);
        Assert.Equal(150, history.TotalDownload);
    }

    [Fact]
    public async Task Refresh_WhileActive_ReloadsProfilesAndFiltersInOneLoad()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);
        history.Activate();
        await history.PendingLoad;
        ui.RunAll();

        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        db.AddUsage(DateTime.Today, "Wi-Fi", "firefox", 50, 5);
        history.Refresh();
        await history.PendingLoad;

        Assert.Equal(2, ui.Count);
        ui.Run(1);

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
        Assert.Equal("Wi-Fi", history.SelectedProfile);
        Assert.Equal(2, history.Rows.Count);
        Assert.Equal(150, history.TotalDownload);
    }

    [Fact]
    public async Task Activate_WithNothingStale_LeavesLoadedRowsAlone()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        await history.PendingLoad;
        var firstRow = history.Rows[0];
        history.Deactivate();

        // Not marked stale, so reopening the tab must not re-query (this row would be replaced by a new instance).
        db.AddUsage(DateTime.Today, "Wi-Fi", "firefox", 50, 5);
        history.Activate();
        await history.PendingLoad;

        Assert.Same(firstRow, Assert.Single(history.Rows));
    }

    [Fact]
    public async Task DeleteAllDbFiles_ClearsLoadedHistoryAndASubsequentReloadStaysEmpty()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        await history.PendingLoad;
        Assert.NotEmpty(history.Rows);
        db.Dispose();

        history.DeleteAllDbFiles();
        history.ReloadProfiles();
        await history.PendingLoad;

        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);
        Assert.Null(history.SelectedProfile);
        Assert.Equal(0, history.TotalDownload);
        Assert.False(history.HasLastFiltered);
    }

    [Fact]
    public async Task DeleteAllDbFiles_DropsAReadThatWasStillWaitingToBeApplied()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);
        history.Activate();
        await history.PendingLoad;
        db.Dispose();

        history.DeleteAllDbFiles();
        // The read finished before the delete; its result must not bring the deleted history back.
        ui.RunAll();

        Assert.Empty(history.Profiles);
        Assert.Empty(history.Rows);
        Assert.Null(history.SelectedProfile);
        Assert.False(history.HasLastFiltered);
    }

    [Fact]
    public async Task Filter_ReturnsTheSameRowsAsTheJoinedQueryItReplaced()
    {
        using var db = UsageReadQueryTests.SeededDb();
        var history = Create(db);
        history.Activate();
        await history.PendingLoad;
        var start = new DateTime(2025, 12, 31);
        var end = new DateTime(2026, 2, 1);

        history.SelectedProfile = "WiFi";
        history.DateStart = new DateTimeOffset(start);
        history.DateEnd = new DateTimeOffset(end);
        history.FilterCommand.Execute(null);
        await history.PendingLoad;

        var expected = UsageReadQueryTests.Rows(db.Connection, UsageReadQueryTests.OldProcessTotalsSql, "WiFi", start, end);
        Assert.Equal(3, expected.Count);
        Assert.Equal(expected, history.Rows.Select(r => $"{r.ProcessName}|{r.DownloadBytes}|{r.UploadBytes}"));
        Assert.Equal(1 + 10 + 100 + 1000 + 7, history.TotalDownload);
        Assert.Equal(2 + 20 + 200 + 2000 + 8, history.TotalUpload);
    }

    [Fact]
    public async Task Filter_AnOlderQueryThatFinishesLateNeverOverwritesANewerOne()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);
        history.Activate();
        await history.PendingLoad;
        ui.RunAll();

        db.AddUsage(DateTime.Today, "Wi-Fi", "firefox", 50, 5);
        history.FilterCommand.Execute(null);
        await history.PendingLoad;
        db.AddUsage(DateTime.Today, "Wi-Fi", "teams", 25, 2);
        history.FilterCommand.Execute(null);
        await history.PendingLoad;
        Assert.Equal(3, ui.Count);

        // The newer result is applied first, then the older one arrives (the order a slow read could produce).
        ui.Run(2);
        Assert.Equal(3, history.Rows.Count);
        ui.Run(1);

        Assert.Equal(3, history.Rows.Count);
        Assert.Equal(175, history.TotalDownload);
    }

    [Fact]
    public async Task Filter_AnOlderQueryIsDroppedEvenWhenItArrivesFirst()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);
        history.Activate();
        await history.PendingLoad;
        ui.RunAll();

        db.AddUsage(DateTime.Today, "Wi-Fi", "firefox", 50, 5);
        history.FilterCommand.Execute(null);
        await history.PendingLoad;
        history.FilterCommand.Execute(null);
        await history.PendingLoad;

        // A newer filter was asked for after this one, so showing this one would only flash stale rows.
        ui.Run(1);
        Assert.Single(history.Rows);

        ui.Run(2);
        Assert.Equal(2, history.Rows.Count);
    }

    [Fact]
    public async Task Filter_ThatOvertakesTheFirstLoad_DoesNotLoseTheSelectionOrTheProfiles()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);
        history.PreferredProfile = "Wi-Fi";

        history.Activate();
        await history.PendingLoad;
        // The Filter button is pressed while the first load is still in flight (nothing is selected yet).
        history.FilterCommand.Execute(null);
        await history.PendingLoad;
        ui.RunAll();

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
        Assert.Equal("Wi-Fi", history.SelectedProfile);
        var row = Assert.Single(history.Rows);
        Assert.Equal("chrome", row.ProcessName);
        Assert.Equal("Wi-Fi", history.LastFilteredProfile);
    }

    [Fact]
    public async Task Filter_ThatOvertakesAProfileReload_StillReloadsTheProfiles()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var ui = new UiQueue();
        var history = Create(db, postToUi: ui.Post);
        history.Activate();
        await history.PendingLoad;
        ui.RunAll();

        db.AddUsage(DateTime.Today, "Ethernet", "teams", 300, 30);
        history.ReloadProfiles();
        await history.PendingLoad;
        history.FilterCommand.Execute(null);
        await history.PendingLoad;
        ui.RunAll();

        Assert.Equal(["Ethernet", "Wi-Fi"], history.Profiles);
        Assert.Equal("Wi-Fi", history.SelectedProfile);
    }

    [Fact]
    public async Task Filter_WithoutAProfile_ClearsTheRowsAndKeepsTheLastFilteredLine()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var history = Create(db);
        history.Activate();
        await history.PendingLoad;
        Assert.Single(history.Rows);

        history.SelectedProfile = null;
        history.FilterCommand.Execute(null);
        await history.PendingLoad;

        Assert.Empty(history.Rows);
        Assert.Equal(0, history.TotalDownload);
        Assert.Equal(0, history.TotalUpload);
        // Nothing was queried, so the line still describes the results that were last loaded.
        Assert.Equal("Wi-Fi", history.LastFilteredProfile);
    }

    [Fact]
    public async Task Rows_StartWithTheCachedIconAndRequestTheRealOne()
    {
        using var db = new TestUsageDb();
        db.AddUsage(DateTime.Today, "Wi-Fi", "chrome", 100, 10);
        var icons = new FakeIconService();
        var history = Create(db, icons);

        history.Activate();
        await history.PendingLoad;

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
