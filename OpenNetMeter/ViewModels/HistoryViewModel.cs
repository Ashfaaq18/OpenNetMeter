using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private readonly string dbPath;
    private readonly IProcessIconService processIconService;
    private readonly IExternalLinkService externalLinkService;
    private readonly IFileSaveDialogService fileSaveDialogService;
    private readonly RelayCommand exportCommand;
    private string? selectedProfile;
    private DateTimeOffset? dateStart;
    private DateTimeOffset? dateEnd;
    private long totalDownload;
    private long totalUpload;
    private string? currentSortColumn;
    private bool sortDescending;
    private bool isExporting;
    private DateTime? lastFilteredAt;
    private string? lastFilteredProfile;
    // History is only read while its tab is showing. Until the first Activate nothing is loaded, and
    // refreshes requested while it is hidden are remembered here and applied on the next Activate.
    private bool isActive;
    private bool hasLoaded;
    private bool profilesStale;
    private bool filterStale;
    private readonly Action<Action> postToUi;
    // The database is read on a background task and the result applied on the UI thread (see RequestLoad). A request
    // made while an earlier one hasn't been applied yet restarts the load with everything asked for so far, and the
    // version drops the results of the earlier reads that finish late.
    private HistoryLoads loadsRequested;
    private int loadVersion;
    private Task pendingLoad = Task.CompletedTask;

    [Flags]
    private enum HistoryLoads
    {
        None = 0,
        // Re-read the profile list, keeping the selection when it still exists.
        Profiles = 1,
        // Re-run the process totals query for the selected profile and date range.
        Filter = 2,
        // The first load: selects the preferred profile (or the first) instead of keeping a selection.
        FirstLoad = 4
    }

    private sealed record LoadRequest(int Version, HistoryLoads Loads, string? SelectedProfile, string? PreferredProfile, DateTime Start, DateTime End);
    private sealed record ProcessUsage(string Name, long Download, long Upload);
    // Rows is null when there was nothing to query (no profile selected, or no database).
    private sealed record FilterResult(string? Profile, List<ProcessUsage>? Rows, DateTime At);
    private sealed record LoadResult(List<string>? Profiles, string? SelectedProfile, FilterResult? Filter);

    public HistoryViewModel()
        : this(new NoOpProcessIconService(), new NoOpExternalLinkService(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService())
    {
    }

    public HistoryViewModel(IProcessIconService processIconService, IExternalLinkService externalLinkService, IFileSaveDialogService fileSaveDialogService, IFileRevealService fileRevealService)
        : this(
            UsageDatabase.ResolveDatabasePath(),
            processIconService,
            externalLinkService,
            fileSaveDialogService,
            fileRevealService,
            action => Dispatcher.UIThread.Post(action, DispatcherPriority.Default))
    {
    }

    // Tests point this at a temporary database and run the "UI thread" by hand.
    internal HistoryViewModel(string dbPath, IProcessIconService processIconService, IExternalLinkService externalLinkService, IFileSaveDialogService fileSaveDialogService, IFileRevealService fileRevealService, Action<Action> postToUi)
    {
        this.postToUi = postToUi;
        this.processIconService = processIconService;
        this.externalLinkService = externalLinkService;
        this.fileSaveDialogService = fileSaveDialogService;
        ExportStatus = new ExportStatusViewModel(fileRevealService);
        this.dbPath = dbPath;

        Profiles = [];
        Rows = [];
        FilterCommand = new RelayCommand(ApplyFilter);
        exportCommand = new RelayCommand(async () => await ExportAsync(), () => !string.IsNullOrWhiteSpace(SelectedProfile) && !isExporting);
        SortRowsCommand = new ParameterRelayCommand(parameter =>
        {
            var column = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(column))
                return;

            SortRows(column);
        });

        // Initial values so the date pickers bind to something; refreshed on the first Activate.
        ResetDateRange();
    }

    public ObservableCollection<string> Profiles { get; }

    /// <summary>
    /// The profile to select on the first <see cref="Activate"/> (the connected adapter, set by the main view model)
    /// when it has history; otherwise the first profile. Later activations keep the user's selection.
    /// </summary>
    public string? PreferredProfile { get; set; }

    public string? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (selectedProfile == value)
                return;
            selectedProfile = value;
            OnPropertyChanged(nameof(SelectedProfile));
            ExportStatus.Clear();
            exportCommand.RaiseCanExecuteChanged();
        }
    }

    public DateTimeOffset? DateStart
    {
        get => dateStart;
        set
        {
            if (dateStart == value)
                return;
            dateStart = value;
            OnPropertyChanged(nameof(DateStart));
        }
    }

    public DateTimeOffset? DateEnd
    {
        get => dateEnd;
        set
        {
            if (dateEnd == value)
                return;
            dateEnd = value;
            OnPropertyChanged(nameof(DateEnd));
        }
    }

    public ObservableCollection<HistoryRowViewModel> Rows { get; }

    public string? CurrentSortColumn => currentSortColumn;
    public bool IsSortDescending => sortDescending;

    public long TotalDownload
    {
        get => totalDownload;
        private set
        {
            if (totalDownload == value)
                return;
            totalDownload = value;
            OnPropertyChanged(nameof(TotalDownload));
            OnPropertyChanged(nameof(TotalDownloadText));
        }
    }

    public long TotalUpload
    {
        get => totalUpload;
        private set
        {
            if (totalUpload == value)
                return;
            totalUpload = value;
            OnPropertyChanged(nameof(TotalUpload));
            OnPropertyChanged(nameof(TotalUploadText));
        }
    }

    public string TotalDownloadText => ByteSizeFormatter.FormatBytes(TotalDownload);
    public string TotalUploadText => ByteSizeFormatter.FormatBytes(TotalUpload);

    /// <summary>
    /// The profile the current results were loaded for; it can differ from <see cref="SelectedProfile"/> until the next filter.
    /// </summary>
    public string LastFilteredProfile => lastFilteredProfile ?? string.Empty;

    /// <summary>
    /// When the current results were loaded, e.g. "22 Oct 2026, 14:05"; empty until a filter has run.
    /// </summary>
    public string LastFilteredTimeText => lastFilteredAt is { } at
        ? DisplayDateFormat.DateTime(at)
        : string.Empty;

    public bool HasLastFiltered => lastFilteredAt != null;

    public ExportStatusViewModel ExportStatus { get; }

    public ICommand FilterCommand { get; }
    public ICommand SortRowsCommand { get; }
    public ICommand ExportCommand => exportCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Called when the History tab becomes visible. The first call loads the profiles and the default
    /// filter (this used to run in the constructor, delaying startup for a tab nobody had opened);
    /// later calls only catch up on refreshes that were requested while the tab was hidden.
    /// </summary>
    public void Activate()
    {
        isActive = true;

        if (!hasLoaded)
        {
            hasLoaded = true;
            profilesStale = false;
            filterStale = false;

            // The app may have been running for days before History was first opened.
            ResetDateRange();

            // Selects the preferred profile (or the first) and filters it, once the profiles are read.
            RequestLoad(HistoryLoads.FirstLoad | HistoryLoads.Profiles | HistoryLoads.Filter);
            return;
        }

        var loads = HistoryLoads.None;
        if (profilesStale)
            loads |= HistoryLoads.Profiles;
        if (filterStale)
            loads |= HistoryLoads.Filter;
        profilesStale = false;
        filterStale = false;

        if (loads != HistoryLoads.None)
            RequestLoad(loads);
    }

    /// <summary>Called when the History tab is left; refreshes are deferred until the next <see cref="Activate"/>.</summary>
    public void Deactivate()
    {
        isActive = false;
    }

    /// <summary>
    /// Re-reads profiles and the current filter, e.g. after old history was purged.
    /// While History is hidden this only marks it stale; the next <see cref="Activate"/> does the work.
    /// </summary>
    public void Refresh()
    {
        if (!isActive)
        {
            profilesStale = true;
            filterStale = true;
            return;
        }

        // One load for both, so the filter runs for the profile that is selected once the list is reloaded.
        RequestLoad(HistoryLoads.Profiles | HistoryLoads.Filter);
    }

    /// <summary>
    /// Re-reads the profile list, keeping the selection when it still exists. While History is hidden this only
    /// marks it stale; the next <see cref="Activate"/> does the work.
    /// </summary>
    public void ReloadProfiles()
    {
        if (!isActive)
        {
            profilesStale = true;
            return;
        }

        RequestLoad(HistoryLoads.Profiles);
    }

    public void DeleteAllDbFiles()
    {
        // Whatever is being read belongs to the data that is about to go; its result must not bring it back.
        loadVersion++;
        loadsRequested = HistoryLoads.None;

        try
        {
            ApplicationDB.CloseSharedConnection();
            // Pooled read-only connections (History, Summary, export) would otherwise keep the file open.
            SqliteConnection.ClearAllPools();
            DeleteIfExists(dbPath);
            DeleteIfExists($"{dbPath}-wal");
            DeleteIfExists($"{dbPath}-shm");
            DeleteIfExists($"{dbPath}-journal");

            Profiles.Clear();
            Rows.Clear();
            SelectedProfile = null;
            TotalDownload = 0;
            TotalUpload = 0;
            SetLastFiltered(null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            EventLogger.Error("Failed to delete usage database file", ex);
        }
    }

    /// <summary>
    /// Completes once the latest request's result has been handed to the UI thread (immediately when nothing is
    /// loading). Lets tests wait for the background part.
    /// </summary>
    internal Task PendingLoad => pendingLoad;

    /// <summary>
    /// Starts reading what <paramref name="loads"/> asks for on a background task; the result is applied on the UI
    /// thread. The request captures what it needs (selection, date range) here, on the UI thread. A request while an
    /// earlier one hasn't been applied yet takes over its work too (<see cref="loadsRequested"/>) and makes the earlier
    /// result stale, so a first load is never lost to a Filter click that overtook it and the user never sees an older
    /// query overwrite a newer one.
    /// </summary>
    private void RequestLoad(HistoryLoads loads)
    {
        loadsRequested |= loads;
        var (start, end) = GetSelectedRange();
        var request = new LoadRequest(++loadVersion, loadsRequested, SelectedProfile, PreferredProfile, start, end);
        pendingLoad = RunLoadAsync(request);
    }

    private async Task RunLoadAsync(LoadRequest request)
    {
        LoadResult result;
        try
        {
            result = await Task.Run(() => ReadHistory(request)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // What was asked for stays requested, so the next request retries it as well.
            EventLogger.Error("Failed to read usage history", ex);
            return;
        }

        postToUi(() => ApplyLoad(request, result));
    }

    /// <summary>The database part of a load. Runs on a background thread and touches no view model state.</summary>
    private LoadResult ReadHistory(LoadRequest request)
    {
        var firstLoad = request.Loads.HasFlag(HistoryLoads.FirstLoad);
        List<string>? profiles = null;
        var selected = request.SelectedProfile;

        if (request.Loads.HasFlag(HistoryLoads.Profiles))
        {
            profiles = ReadProfiles();

            // The first load goes to the connected adapter's profile; a later one keeps the user's selection.
            var wanted = firstLoad ? request.PreferredProfile : request.SelectedProfile;
            selected = !string.IsNullOrWhiteSpace(wanted) && profiles.Contains(wanted)
                ? wanted
                : profiles.FirstOrDefault();
        }

        // A first load with no profile at all has nothing to select, so nothing to filter.
        FilterResult? filter = null;
        if (request.Loads.HasFlag(HistoryLoads.Filter) && !(firstLoad && profiles is { Count: 0 }))
            filter = ReadProcessTotals(selected, request.Start, request.End);

        return new LoadResult(profiles, selected, filter);
    }

    /// <summary>The UI-thread part of a load: shows what <see cref="ReadHistory"/> found, unless a newer request has taken over.</summary>
    private void ApplyLoad(LoadRequest request, LoadResult result)
    {
        if (request.Version != loadVersion)
            return;

        loadsRequested = HistoryLoads.None;

        if (result.Profiles is { } profiles)
        {
            // Read before the list is rebuilt: clearing it makes the ComboBox write null back to SelectedProfile.
            var current = SelectedProfile;
            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(profile);

            if (request.Loads.HasFlag(HistoryLoads.FirstLoad))
            {
                if (profiles.Count > 0)
                    SelectedProfile = result.SelectedProfile;
            }
            else
            {
                // The user may have picked another profile while this was loading; that choice stays if it still exists.
                SelectedProfile = !string.IsNullOrWhiteSpace(current) && profiles.Contains(current)
                    ? current
                    : result.SelectedProfile;
            }
        }

        if (result.Filter is { } filter)
            ApplyFilterResult(filter);
    }

    private List<string> ReadProfiles()
    {
        var profiles = new List<string>();
        if (!File.Exists(dbPath))
            return profiles;

        using var connection = UsageDatabase.OpenReadOnlyConnection(dbPath);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Adapter ORDER BY Name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                profiles.Add(reader.GetString(0));
        }

        return profiles;
    }

    // Filters ProcessDate by adapter id and date ids so it can use IX_ProcessDate_AdapterID_DateID instead of
    // joining (and scanning) every row. Adapter.Name is unique, so the subquery yields at most one id.
    internal const string ProcessTotalsSql =
        "SELECT p.Name, SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
        "FROM ProcessDate pd " +
        "JOIN Process p ON p.ID = pd.ProcessID " +
        "WHERE pd.AdapterID = (SELECT ID FROM Adapter WHERE Name = @AdapterName) " +
        "AND pd.DateID IN (SELECT ID FROM Date " +
        "WHERE (Year * 10000 + Month * 100 + Day) BETWEEN @StartDate AND @EndDate) " +
        "GROUP BY p.ID, p.Name " +
        "ORDER BY p.Name";

    private void ApplyFilter() => RequestLoad(HistoryLoads.Filter);

    /// <summary>The per-process totals of one profile over a date range; Rows is null when there is nothing to query.</summary>
    private FilterResult ReadProcessTotals(string? profile, DateTime start, DateTime end)
    {
        if (string.IsNullOrWhiteSpace(profile) || !File.Exists(dbPath))
            return new FilterResult(profile, null, default);

        var rows = new List<ProcessUsage>();

        using var connection = UsageDatabase.OpenReadOnlyConnection(dbPath);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = ProcessTotalsSql;
        command.Parameters.AddWithValue("@AdapterName", profile);
        command.Parameters.AddWithValue("@StartDate", UsageDatabase.ToDateInt(start));
        command.Parameters.AddWithValue("@EndDate", UsageDatabase.ToDateInt(end));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var processName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var download = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            var upload = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
            rows.Add(new ProcessUsage(processName, download, upload));
        }

        return new FilterResult(profile, rows, DateTime.Now);
    }

    private void ApplyFilterResult(FilterResult filter)
    {
        Rows.Clear();

        if (filter.Rows is not { } usage)
        {
            // No profile selected, or no database: nothing to show, and the "Last filtered" line stays as it was.
            TotalDownload = 0;
            TotalUpload = 0;
            return;
        }

        long download = 0;
        long upload = 0;
        foreach (var item in usage)
        {
            // The cached/default icon keeps this loop off the process-snapshot path; real icons arrive in the background.
            var row = new HistoryRowViewModel(item.Name, item.Download, item.Upload, externalLinkService, processIconService.GetCachedIcon(item.Name) as IImage);
            ProcessIconLoader.Attach(processIconService, item.Name, icon => row.Icon = icon, postToUi);
            Rows.Add(row);
            download += item.Download;
            upload += item.Upload;
        }

        TotalDownload = download;
        TotalUpload = upload;
        SetLastFiltered(filter.Profile, filter.At);
    }

    private void ResetDateRange()
    {
        DateStart = DateTimeOffset.Now.Date.AddDays(-7);
        DateEnd = DateTimeOffset.Now.Date;
    }

    private void SetLastFiltered(string? profile, DateTime? at)
    {
        if (lastFilteredProfile == profile && lastFilteredAt == at)
            return;
        lastFilteredProfile = profile;
        lastFilteredAt = at;
        OnPropertyChanged(nameof(LastFilteredProfile));
        OnPropertyChanged(nameof(LastFilteredTimeText));
        OnPropertyChanged(nameof(HasLastFiltered));
    }

    private void SortRows(string column)
    {
        sortDescending = string.Equals(currentSortColumn, column, StringComparison.Ordinal) ? !sortDescending : false;
        currentSortColumn = column;

        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(IsSortDescending));

        var sorted = column switch
        {
            "Download" => sortDescending
                ? Rows.OrderByDescending(r => r.DownloadBytes).ToList()
                : Rows.OrderBy(r => r.DownloadBytes).ToList(),
            "Upload" => sortDescending
                ? Rows.OrderByDescending(r => r.UploadBytes).ToList()
                : Rows.OrderBy(r => r.UploadBytes).ToList(),
            _ => sortDescending
                ? Rows.OrderByDescending(r => r.ProcessName).ToList()
                : Rows.OrderBy(r => r.ProcessName).ToList()
        };

        Rows.Clear();
        foreach (var row in sorted)
            Rows.Add(row);
    }

    private (DateTime Start, DateTime End) GetSelectedRange()
    {
        var start = (DateStart ?? DateTimeOffset.Now.Date).Date;
        var end = (DateEnd ?? DateTimeOffset.Now.Date).Date;
        return end < start ? (end, start) : (start, end);
    }

    private async Task ExportAsync()
    {
        var profile = SelectedProfile;
        if (string.IsNullOrWhiteSpace(profile) || isExporting)
            return;

        isExporting = true;
        ExportStatus.Clear();
        exportCommand.RaiseCanExecuteChanged();
        try
        {
            var (start, end) = GetSelectedRange();
            string? path = await fileSaveDialogService.PickSavePathAsync(
                "Export usage history",
                SanitizeFileName($"OpenNetMeter-{profile}-{start:yyyyMMdd}-{end:yyyyMMdd}"),
                UsageExporter.FileTypes);
            if (string.IsNullOrWhiteSpace(path))
                return;

            ExportStatus.ShowProgress();
            int count = await UsageExporter.ExportAsync(dbPath, path, profile, start, end);
            ExportStatus.ShowSuccess(count, path);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Error exporting usage history", ex);
            ExportStatus.ShowError();
        }
        finally
        {
            isExporting = false;
            exportCommand.RaiseCanExecuteChanged();
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class NoOpProcessIconService : IProcessIconService
    {
        public object? GetCachedIcon(string processName) => null;
        public Task<object?> LoadIconAsync(string processName) => Task.FromResult<object?>(null);
    }

    private sealed class NoOpExternalLinkService : IExternalLinkService
    {
        public void Open(string uri) { }
    }
}

public sealed class HistoryRowViewModel : INotifyPropertyChanged
{
    private readonly IExternalLinkService externalLinkService;
    private IImage? icon;

    public HistoryRowViewModel(string processName, long downloadBytes, long uploadBytes, IExternalLinkService externalLinkService, IImage? icon = null)
    {
        ProcessName = processName;
        DownloadBytes = downloadBytes;
        UploadBytes = uploadBytes;
        this.externalLinkService = externalLinkService;
        this.icon = icon;
        SearchProcessCommand = new RelayCommand(() =>
        {
            var encodedName = Uri.EscapeDataString(ProcessName);
            externalLinkService.Open($"https://www.google.com/search?q={encodedName}");
        });
    }

    public IImage? Icon
    {
        get => icon;
        set
        {
            if (ReferenceEquals(icon, value))
                return;
            icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    public string ProcessName { get; }
    public long DownloadBytes { get; }
    public long UploadBytes { get; }
    public ICommand SearchProcessCommand { get; }
    public string DownloadText => ByteSizeFormatter.FormatBytes(DownloadBytes);
    public string UploadText => ByteSizeFormatter.FormatBytes(UploadBytes);

    public event PropertyChangedEventHandler? PropertyChanged;
}

