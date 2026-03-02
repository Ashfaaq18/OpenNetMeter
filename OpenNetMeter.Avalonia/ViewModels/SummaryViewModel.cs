using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

    private long currentSessionDownload;
    private long currentSessionUpload;
    private long totalFromDateDownload;
    private long totalFromDateUpload;

    // LiveCharts observable collections
    private readonly ObservableCollection<ObservablePoint> _dlValues = new();
    private readonly ObservableCollection<ObservablePoint> _ulValues = new();
    private const int WindowSize = 35; // ~30 sec at 850ms tick
    private int _tickCount;

    public SummaryViewModel()
    {
        var dlColor = new SKColor(0x2E, 0xA0, 0x43);
        var ulColor = new SKColor(0x4E, 0xA1, 0xFF);

        GraphSeries =
        [
            new LineSeries<ObservablePoint>
            {
                Values = _dlValues,
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
                Values = _ulValues,
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

        ActiveProcesses =
        [
            new("chrome", "2.1 MB", "1.0 MB", "142 MB", "68 MB"),
            new("discord", "620 KB", "190 KB", "18 MB", "9 MB"),
            new("steam", "8.4 MB", "700 KB", "1.2 GB", "122 MB"),
            new("system", "120 KB", "95 KB", "12 MB", "9 MB")
        ];

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(850)
        };
        timer.Tick += (_, _) => Tick();
        timer.Start();

        Tick();
    }

    // ── LiveCharts bindings ────────────────────────────────

    public ISeries[] GraphSeries { get; }
    public Axis[] GraphXAxes { get; }
    public Axis[] GraphYAxes { get; }

    // ── Existing properties ────────────────────────────────

    public string CurrentSessionDownloadText => FormatBytes(currentSessionDownload);
    public string CurrentSessionUploadText => FormatBytes(currentSessionUpload);
    public string TotalFromDateDownloadText => FormatBytes(totalFromDateDownload);
    public string TotalFromDateUploadText => FormatBytes(totalFromDateUpload);

    public ObservableCollection<SummaryProcessRowViewModel> ActiveProcesses { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Tick()
    {
        double nextDownloadMbps = Math.Round(2.0 + random.NextDouble() * 18.0, 1);
        double nextUploadMbps = Math.Round(0.3 + random.NextDouble() * 7.0, 1);

        // Push to LiveCharts with explicit X position
        _dlValues.Add(new ObservablePoint(_tickCount, nextDownloadMbps));
        _ulValues.Add(new ObservablePoint(_tickCount, nextUploadMbps));

        // Remove old points outside the window
        while (_dlValues.Count > WindowSize)
            _dlValues.RemoveAt(0);
        while (_ulValues.Count > WindowSize)
            _ulValues.RemoveAt(0);

        // Slide the visible window
        if (_tickCount >= WindowSize)
        {
            GraphXAxes[0].MinLimit = _tickCount - WindowSize;
            GraphXAxes[0].MaxLimit = _tickCount;
        }

        _tickCount++;

        // Update cumulative stats
        currentSessionDownload += (long)(nextDownloadMbps * 125_000);
        currentSessionUpload += (long)(nextUploadMbps * 125_000);
        totalFromDateDownload += (long)(nextDownloadMbps * 125_000);
        totalFromDateUpload += (long)(nextUploadMbps * 125_000);

        OnPropertyChanged(nameof(CurrentSessionDownloadText));
        OnPropertyChanged(nameof(CurrentSessionUploadText));
        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));
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