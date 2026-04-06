using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia.Media;
using Microsoft.Data.Sqlite;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private readonly string dbPath;
    private readonly IProcessIconService processIconService;
    private string? selectedProfile;
    private DateTimeOffset? dateStart;
    private DateTimeOffset? dateEnd;
    private long totalDownload;
    private long totalUpload;
    private string? currentSortColumn;
    private bool sortDescending;

    public HistoryViewModel()
        : this(new NoOpProcessIconService())
    {
    }

    public HistoryViewModel(IProcessIconService processIconService)
    {
        this.processIconService = processIconService;
        dbPath = ResolveDatabasePath();

        Profiles = [];
        Rows = [];
        FilterCommand = new RelayCommand(ApplyFilter);
        SortRowsCommand = new ParameterRelayCommand(parameter =>
        {
            var column = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(column))
                return;

            SortRows(column);
        });

        DateStart = DateTimeOffset.Now.Date.AddDays(-7);
        DateEnd = DateTimeOffset.Now.Date;

        LoadProfiles();
        if (Profiles.Count > 0)
        {
            SelectedProfile = Profiles[0];
            ApplyFilter();
        }
    }

    public ObservableCollection<string> Profiles { get; }

    public string? SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (selectedProfile == value)
                return;
            selectedProfile = value;
            OnPropertyChanged(nameof(SelectedProfile));
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

    public ICommand FilterCommand { get; }
    public ICommand SortRowsCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ReloadProfiles()
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
            DeleteIfExists(dbPath);
            DeleteIfExists($"{dbPath}-wal");
            DeleteIfExists($"{dbPath}-shm");
            DeleteIfExists($"{dbPath}-journal");

            Profiles.Clear();
            Rows.Clear();
            SelectedProfile = null;
            TotalDownload = 0;
            TotalUpload = 0;
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

        using var connection = OpenReadOnlyConnection(dbPath);
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

        if (string.IsNullOrWhiteSpace(SelectedProfile) || !File.Exists(dbPath))
            return;

        var start = (DateStart ?? DateTimeOffset.Now.Date).Date;
        var end = (DateEnd ?? DateTimeOffset.Now.Date).Date;
        if (end < start)
            (start, end) = (end, start);

        using var connection = OpenReadOnlyConnection(dbPath);
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
        command.Parameters.AddWithValue("@AdapterName", SelectedProfile);
        command.Parameters.AddWithValue("@StartDate", ToDateInt(start));
        command.Parameters.AddWithValue("@EndDate", ToDateInt(end));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var processName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var download = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
            var upload = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);

            var icon = processIconService.GetProcessIcon(processName) as IImage;
            Rows.Add(new HistoryRowViewModel(processName, download, upload, icon));
            TotalDownload += download;
            TotalUpload += upload;
        }
    }

    private void SortRows(string column)
    {
        if (string.Equals(currentSortColumn, column, StringComparison.Ordinal))
        {
            sortDescending = !sortDescending;
        }
        else
        {
            currentSortColumn = column;
            sortDescending = false;
        }

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

    private static SqliteConnection OpenReadOnlyConnection(string path)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        };
        return new SqliteConnection(csb.ToString());
    }

    private static int ToDateInt(DateTime date)
    {
        return (date.Year * 10000) + (date.Month * 100) + date.Day;
    }

    private static string ResolveDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "OpenNetMeter");
        return Path.Combine(appFolder, "OpenNetMeter.sqlite");
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
        public object? GetProcessIcon(string processName) => null;
    }
}

public sealed class HistoryRowViewModel
{
    public HistoryRowViewModel(string processName, long downloadBytes, long uploadBytes, IImage? icon = null)
    {
        ProcessName = processName;
        DownloadBytes = downloadBytes;
        UploadBytes = uploadBytes;
        Icon = icon;
    }

    public IImage? Icon { get; }
    public string ProcessName { get; }
    public long DownloadBytes { get; }
    public long UploadBytes { get; }
    public string DownloadText => ByteSizeFormatter.FormatBytes(DownloadBytes);
    public string UploadText => ByteSizeFormatter.FormatBytes(UploadBytes);
}

