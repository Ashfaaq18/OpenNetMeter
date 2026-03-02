using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class SummaryViewModel : INotifyPropertyChanged
{
    private readonly Random random = new();
    private readonly DispatcherTimer timer;
    private readonly double[] downloadSamples = new double[40];
    private readonly double[] uploadSamples = new double[40];

    private long currentSessionDownload;
    private long currentSessionUpload;
    private long totalFromDateDownload;
    private long totalFromDateUpload;
    private string downloadSpeedText = "0 Mbps";
    private string uploadSpeedText = "0 Mbps";
    private string downloadPolylinePoints = string.Empty;
    private string uploadPolylinePoints = string.Empty;

    public SummaryViewModel()
    {
        ActiveProcesses = new ObservableCollection<SummaryProcessRowViewModel>
        {
            new("chrome", "2.1 MB", "1.0 MB", "142 MB", "68 MB"),
            new("discord", "620 KB", "190 KB", "18 MB", "9 MB"),
            new("steam", "8.4 MB", "700 KB", "1.2 GB", "122 MB"),
            new("system", "120 KB", "95 KB", "12 MB", "9 MB")
        };

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(850)
        };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        Tick();
    }

    public string DownloadSpeedText
    {
        get => downloadSpeedText;
        private set
        {
            if (downloadSpeedText == value)
                return;
            downloadSpeedText = value;
            OnPropertyChanged(nameof(DownloadSpeedText));
        }
    }

    public string UploadSpeedText
    {
        get => uploadSpeedText;
        private set
        {
            if (uploadSpeedText == value)
                return;
            uploadSpeedText = value;
            OnPropertyChanged(nameof(UploadSpeedText));
        }
    }

    public string CurrentSessionDownloadText => FormatBytes(currentSessionDownload);
    public string CurrentSessionUploadText => FormatBytes(currentSessionUpload);
    public string TotalFromDateDownloadText => FormatBytes(totalFromDateDownload);
    public string TotalFromDateUploadText => FormatBytes(totalFromDateUpload);

    public string DownloadPolylinePoints
    {
        get => downloadPolylinePoints;
        private set
        {
            if (downloadPolylinePoints == value)
                return;
            downloadPolylinePoints = value;
            OnPropertyChanged(nameof(DownloadPolylinePoints));
        }
    }

    public string UploadPolylinePoints
    {
        get => uploadPolylinePoints;
        private set
        {
            if (uploadPolylinePoints == value)
                return;
            uploadPolylinePoints = value;
            OnPropertyChanged(nameof(UploadPolylinePoints));
        }
    }

    public ObservableCollection<SummaryProcessRowViewModel> ActiveProcesses { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Tick()
    {
        double nextDownloadMbps = Math.Round(2.0 + random.NextDouble() * 18.0, 1);
        double nextUploadMbps = Math.Round(0.3 + random.NextDouble() * 7.0, 1);

        Shift(downloadSamples, nextDownloadMbps);
        Shift(uploadSamples, nextUploadMbps);

        DownloadSpeedText = $"{nextDownloadMbps:0.0} Mbps";
        UploadSpeedText = $"{nextUploadMbps:0.0} Mbps";

        currentSessionDownload += (long)(nextDownloadMbps * 125_000);
        currentSessionUpload += (long)(nextUploadMbps * 125_000);
        totalFromDateDownload += (long)(nextDownloadMbps * 125_000);
        totalFromDateUpload += (long)(nextUploadMbps * 125_000);

        OnPropertyChanged(nameof(CurrentSessionDownloadText));
        OnPropertyChanged(nameof(CurrentSessionUploadText));
        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));

        DownloadPolylinePoints = BuildPolyline(downloadSamples, 760, 190, 20);
        UploadPolylinePoints = BuildPolyline(uploadSamples, 760, 190, 20);
    }

    private static void Shift(double[] data, double next)
    {
        for (int i = 0; i < data.Length - 1; i++)
            data[i] = data[i + 1];
        data[^1] = next;
    }

    private static string BuildPolyline(double[] samples, double width, double height, double maxValue)
    {
        double stepX = width / (samples.Length - 1);
        var points = new string[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            double x = i * stepX;
            double normalized = Math.Clamp(samples[i] / maxValue, 0, 1);
            double y = height - normalized * height;
            points[i] = $"{x:0.##},{y:0.##}";
        }
        return string.Join(" ", points);
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

public sealed class SummaryProcessRowViewModel
{
    public SummaryProcessRowViewModel(
        string processName,
        string currentDownload,
        string currentUpload,
        string totalDownload,
        string totalUpload)
    {
        ProcessName = processName;
        CurrentDownload = currentDownload;
        CurrentUpload = currentUpload;
        TotalDownload = totalDownload;
        TotalUpload = totalUpload;
    }

    public string ProcessName { get; }
    public string CurrentDownload { get; }
    public string CurrentUpload { get; }
    public string TotalDownload { get; }
    public string TotalUpload { get; }
}
