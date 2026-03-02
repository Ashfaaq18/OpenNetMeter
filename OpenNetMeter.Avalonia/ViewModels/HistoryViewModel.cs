using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private string? selectedProfile;
    private DateTimeOffset? dateStart;
    private DateTimeOffset? dateEnd;
    private long totalDownload;
    private long totalUpload;

    public HistoryViewModel()
    {
        Profiles = new ObservableCollection<string>
        {
            "Primary Adapter",
            "Wi-Fi Adapter",
            "Ethernet Adapter"
        };

        SelectedProfile = Profiles.FirstOrDefault();
        DateStart = DateTimeOffset.Now.Date.AddDays(-7);
        DateEnd = DateTimeOffset.Now.Date;

        Rows = new ObservableCollection<HistoryRowViewModel>();
        FilterCommand = new RelayCommand(ApplyFilter);
        ApplyFilter();
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

    public string TotalDownloadText => FormatBytes(TotalDownload);
    public string TotalUploadText => FormatBytes(TotalUpload);

    public ICommand FilterCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void ApplyFilter()
    {
        Rows.Clear();

        var start = DateStart?.Date ?? DateTimeOffset.Now.Date.AddDays(-7);
        var end = DateEnd?.Date ?? DateTimeOffset.Now.Date;
        if (end < start)
            (start, end) = (end, start);

        int days = Math.Max(1, (end - start).Days + 1);
        int profileFactor = Math.Abs((SelectedProfile ?? "default").GetHashCode()) % 5 + 1;

        AddRow("chrome", days, 42_000L * profileFactor);
        AddRow("discord", days, 28_000L * (profileFactor + 1));
        AddRow("steam", days, 65_000L * (profileFactor + 2));
        AddRow("system", days, 12_000L * (profileFactor + 3));

        TotalDownload = Rows.Sum(r => r.DownloadBytes);
        TotalUpload = Rows.Sum(r => r.UploadBytes);
    }

    private void AddRow(string name, int days, long baseValue)
    {
        long download = baseValue * days;
        long upload = (baseValue / 2) * days;
        Rows.Add(new HistoryRowViewModel(name, download, upload));
    }

    private static string FormatBytes(long value)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        double current = value;
        int unit = 0;
        while (current >= 1024 && unit < suffix.Length - 1)
        {
            current /= 1024;
            unit++;
        }
        return $"{current:0.##} {suffix[unit]}";
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class HistoryRowViewModel
{
    public HistoryRowViewModel(string processName, long downloadBytes, long uploadBytes)
    {
        ProcessName = processName;
        DownloadBytes = downloadBytes;
        UploadBytes = uploadBytes;
    }

    public string ProcessName { get; }
    public long DownloadBytes { get; }
    public long UploadBytes { get; }
    public string DownloadText => FormatBytes(DownloadBytes);
    public string UploadText => FormatBytes(UploadBytes);

    private static string FormatBytes(long value)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        double current = value;
        int unit = 0;
        while (current >= 1024 && unit < suffix.Length - 1)
        {
            current /= 1024;
            unit++;
        }
        return $"{current:0.##} {suffix[unit]}";
    }
}
