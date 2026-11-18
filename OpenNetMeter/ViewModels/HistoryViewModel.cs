using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
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

    public HistoryViewModel()
        : this(new NoOpProcessIconService(), new NoOpExternalLinkService(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService())
    {
    }

    public HistoryViewModel(IProcessIconService processIconService, IExternalLinkService externalLinkService, IFileSaveDialogService fileSaveDialogService, IFileRevealService fileRevealService)
        : this(UsageDatabase.ResolveDatabasePath(), processIconService, externalLinkService, fileSaveDialogService, fileRevealService)
    {
    }

    // Tests point this at a temporary database.
    internal HistoryViewModel(string dbPath, IProcessIconService processIconService, IExternalLinkService externalLinkService, IFileSaveDialogService fileSaveDialogService, IFileRevealService fileRevealService)
    {
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

            LoadProfiles();
            if (Profiles.Count > 0)
            {
                SelectedProfile = PreferredProfile is { } preferred && Profiles.Contains(preferred)
                    ? preferred
                    : Profiles[0];
                ApplyFilter();
            }

            return;
        }

        var reloadProfiles = profilesStale;
        var reapplyFilter = filterStale;
        profilesStale = false;
        filterStale = false;

        if (reloadProfiles)
            ReloadProfilesNow();
        if (reapplyFilter)
            ApplyFilter();
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

        ReloadProfilesNow();
        ApplyFilter();
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

        ReloadProfilesNow();
    }

    private void ReloadProfilesNow()
    {
        string? previousSelection = SelectedProfile;
        LoadProfiles();

        if (!string.IsNullOrWhiteSpace(previousSelection) && Profiles.Contains(previousSelection))
        {
            SelectedProfile = previousSelection;
        }
        else
        {
            SelectedProfile = Profiles.Count > 0 ? Profiles[0] : null;
        }
    }

    public void DeleteAllDbFiles()
    {
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

    private void LoadProfiles()
    {
        Profiles.Clear();

        if (!File.Exists(dbPath))
            return;

        using var connection = UsageDatabase.OpenReadOnlyConnection(dbPath);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Adapter ORDER BY Name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
                Profiles.Add(reader.GetString(0));
        }
    }

    private void ApplyFilter()
    {
        Rows.Clear();
        TotalDownload = 0;
        TotalUpload = 0;

        // Read once: the same profile feeds the query and the "Last filtered" line.
        var profile = SelectedProfile;
        if (string.IsNullOrWhiteSpace(profile) || !File.Exists(dbPath))
            return;

        var (start, end) = GetSelectedRange();

        using var connection = UsageDatabase.OpenReadOnlyConnection(dbPath);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT p.Name, SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
            "FROM ProcessDate pd " +
            "JOIN Process p ON p.ID = pd.ProcessID " +
            "JOIN Adapter a ON a.ID = pd.AdapterID " +
            "JOIN Date d ON d.ID = pd.DateID " +
            "WHERE a.Name = @AdapterName " +
            "AND (d.Year * 10000 + d.Month * 100 + d.Day) BETWEEN @StartDate AND @EndDate " +
            "GROUP BY p.ID, p.Name " +
            "ORDER BY p.Name";
        command.Parameters.AddWithValue("@AdapterName", profile);
        command.Parameters.AddWithValue("@StartDate", UsageDatabase.ToDateInt(start));
        command.Parameters.AddWithValue("@EndDate", UsageDatabase.ToDateInt(end));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var processName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var download = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            var upload = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);

            // The cached/default icon keeps this loop off the process-snapshot path; real icons arrive in the background.
            var row = new HistoryRowViewModel(processName, download, upload, externalLinkService, processIconService.GetCachedIcon(processName) as IImage);
            ProcessIconLoader.Attach(processIconService, processName, icon => row.Icon = icon);
            Rows.Add(row);
            TotalDownload += download;
            TotalUpload += upload;
        }

        SetLastFiltered(profile, DateTime.Now);
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

