using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class SummaryViewModel : INotifyPropertyChanged
{
    private readonly Random random = new();
    private readonly DispatcherTimer timer;
    private readonly ObservableCollection<ObservablePoint> dlValues = new();
    private readonly ObservableCollection<ObservablePoint> ulValues = new();
    private string? currentSortColumn;
    private bool sortDescending;

    private const int WindowSize = 35;
    private int tickCount;
    private long currentSessionDownload;
    private long currentSessionUpload;
    private long totalFromDateDownload;
    private long totalFromDateUpload;
    private double latestDownloadMbps;
    private double latestUploadMbps;
    private DateTimeOffset? sinceDate;

    public SummaryViewModel()
    {
        var dlColor = new SKColor(0x2E, 0xA0, 0x43);
        var ulColor = new SKColor(0x4E, 0xA1, 0xFF);

        GraphSeries =
        [
            new LineSeries<ObservablePoint>
            {
                Values = dlValues,
                Stroke = new SolidColorPaint(dlColor, 2),
                GeometrySize = 0,
                GeometryStroke = null,
                GeometryFill = null,
                Fill = new SolidColorPaint(dlColor.WithAlpha(0x33)),
                LineSmoothness = 0.3,
                Name = "Download"
            },
            new LineSeries<ObservablePoint>
            {
                Values = ulValues,
                Stroke = new SolidColorPaint(ulColor, 2),
                GeometrySize = 0,
                GeometryStroke = null,
                GeometryFill = null,
                Fill = new SolidColorPaint(ulColor.WithAlpha(0x33)),
                LineSmoothness = 0.3,
                Name = "Upload"
            }
        ];

        GraphXAxes =
        [
            new Axis
            {
                ShowSeparatorLines = false,
                IsVisible = false,
                MinLimit = 0,
                MaxLimit = WindowSize
            }
        ];

        GraphYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x28, 0x32, 0x41)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(new SKColor(0x88, 0x99, 0xAA)),
                TextSize = 10,
                Labeler = v =>
                {
                    if (v < 1) return $"{v:0.0} Mbps";
                    if (v < 10) return $"{v:0.#} Mbps";
                    return $"{v:0} Mbps";
                }
            }
        ];

        ActiveProcesses = [];
        for (var i = 1; i <= 40; i++)
        {
            var currentDownKb = 120 + (i * 37 % 900);
            var currentUpKb = 80 + (i * 23 % 700);
            var totalDownMb = 25 + (i * 17 % 2400);
            var totalUpMb = 10 + (i * 13 % 1300);

            ActiveProcesses.Add(new SummaryProcessRowViewModel(
                $"process-{i:00}",
                $"{currentDownKb} KB",
                $"{currentUpKb} KB",
                $"{totalDownMb} MB",
                $"{totalUpMb} MB"));
        }

        SortProcessesCommand = new ParameterRelayCommand(parameter =>
        {
            var column = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(column))
                return;

            SortProcesses(column);
        });

        sinceDate = DateTimeOffset.Now.Date.AddDays(-7);

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(850)
        };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        Tick();
    }

    public ISeries[] GraphSeries { get; }
    public Axis[] GraphXAxes { get; }
    public Axis[] GraphYAxes { get; }

    public string CurrentSessionDownloadText => FormatBytes(currentSessionDownload);
    public string CurrentSessionUploadText => FormatBytes(currentSessionUpload);
    public string TotalFromDateDownloadText => FormatBytes(totalFromDateDownload);
    public string TotalFromDateUploadText => FormatBytes(totalFromDateUpload);
    public string DownloadSpeedText => $"{latestDownloadMbps:0.0} Mbps";
    public string UploadSpeedText => $"{latestUploadMbps:0.0} Mbps";
    public int ProcessCount => ActiveProcesses.Count;
    public ICommand SortProcessesCommand { get; }

    public DateTimeOffset? SinceDate
    {
        get => sinceDate;
        set
        {
            if (sinceDate == value)
                return;

            sinceDate = value;
            OnPropertyChanged(nameof(SinceDate));
        }
    }

    public ObservableCollection<SummaryProcessRowViewModel> ActiveProcesses { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Tick()
    {
        var nextDownloadMbps = Math.Round(2.0 + random.NextDouble() * 18.0, 1);
        var nextUploadMbps = Math.Round(0.3 + random.NextDouble() * 7.0, 1);

        dlValues.Add(new ObservablePoint(tickCount, nextDownloadMbps));
        ulValues.Add(new ObservablePoint(tickCount, nextUploadMbps));

        while (dlValues.Count > WindowSize)
            dlValues.RemoveAt(0);
        while (ulValues.Count > WindowSize)
            ulValues.RemoveAt(0);

        if (tickCount >= WindowSize)
        {
            GraphXAxes[0].MinLimit = tickCount - WindowSize;
            GraphXAxes[0].MaxLimit = tickCount;
        }

        tickCount++;

        latestDownloadMbps = nextDownloadMbps;
        latestUploadMbps = nextUploadMbps;
        currentSessionDownload += (long)(nextDownloadMbps * 125_000);
        currentSessionUpload += (long)(nextUploadMbps * 125_000);
        totalFromDateDownload += (long)(nextDownloadMbps * 125_000);
        totalFromDateUpload += (long)(nextUploadMbps * 125_000);

        OnPropertyChanged(nameof(CurrentSessionDownloadText));
        OnPropertyChanged(nameof(CurrentSessionUploadText));
        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
    }

    private void SortProcesses(string column)
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

        Func<SummaryProcessRowViewModel, IComparable> selector = column switch
        {
            "Name" => p => p.ProcessName,
            "CurrentDownload" => p => ParseDataSizeToBytes(p.CurrentDownload),
            "CurrentUpload" => p => ParseDataSizeToBytes(p.CurrentUpload),
            "TotalDownload" => p => ParseDataSizeToBytes(p.TotalDownload),
            "TotalUpload" => p => ParseDataSizeToBytes(p.TotalUpload),
            _ => p => p.ProcessName
        };

        var sorted = sortDescending
            ? ActiveProcesses.OrderByDescending(selector).ToList()
            : ActiveProcesses.OrderBy(selector).ToList();

        ActiveProcesses.Clear();
        foreach (var item in sorted)
            ActiveProcesses.Add(item);
    }

    private static long ParseDataSizeToBytes(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !double.TryParse(parts[0], out var size))
            return 0;

        var unit = parts.Length > 1 ? parts[1].ToUpperInvariant() : "B";
        var multiplier = unit switch
        {
            "TB" => 1024d * 1024d * 1024d * 1024d,
            "GB" => 1024d * 1024d * 1024d,
            "MB" => 1024d * 1024d,
            "KB" => 1024d,
            _ => 1d
        };

        return (long)(size * multiplier);
    }

    private static string FormatBytes(long value)
    {
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        var current = (double)value;
        var unit = 0;
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

    private sealed class ParameterRelayCommand : ICommand
    {
        private readonly Action<object?> execute;

        public ParameterRelayCommand(Action<object?> execute)
        {
            this.execute = execute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            execute(parameter);
        }
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

    public object? Icon => null;
    public string ProcessName { get; }
    public string CurrentDownload { get; }
    public string CurrentUpload { get; }
    public string TotalDownload { get; }
    public string TotalUpload { get; }
}
