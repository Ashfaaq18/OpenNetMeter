using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using OpenNetMeter.PlatformAbstractions;
using SkiaSharp;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class SummaryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly INetworkCaptureService networkCaptureService;
    private readonly ObservableCollection<ObservablePoint> dlValues = new();
    private readonly ObservableCollection<ObservablePoint> ulValues = new();
    private readonly Dictionary<string, SummaryProcessRowViewModel> processIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object pendingLock = new();
    private readonly Dictionary<string, PendingTraffic> pendingByProcess = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer flushTimer;
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
    private long pendingDownloadBytes;
    private long pendingUploadBytes;

    public SummaryViewModel(INetworkCaptureService networkCaptureService)
    {
        this.networkCaptureService = networkCaptureService;

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
        SortProcessesCommand = new ParameterRelayCommand(parameter =>
        {
            var column = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(column))
                return;

            SortProcesses(column);
        });

        sinceDate = DateTimeOffset.Now.Date;
        this.networkCaptureService.TrafficObserved += OnTrafficObserved;

        flushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        flushTimer.Tick += (_, _) => FlushPendingTraffic();
        flushTimer.Start();
    }

    public ISeries[] GraphSeries { get; }
    public Axis[] GraphXAxes { get; }
    public Axis[] GraphYAxes { get; }
    public ObservableCollection<SummaryProcessRowViewModel> ActiveProcesses { get; }
    public ICommand SortProcessesCommand { get; }

    public string CurrentSessionDownloadText => FormatBytes(currentSessionDownload);
    public string CurrentSessionUploadText => FormatBytes(currentSessionUpload);
    public string TotalFromDateDownloadText => FormatBytes(totalFromDateDownload);
    public string TotalFromDateUploadText => FormatBytes(totalFromDateUpload);
    public string DownloadSpeedText => $"{latestDownloadMbps:0.0} Mbps";
    public string UploadSpeedText => $"{latestUploadMbps:0.0} Mbps";
    public int ProcessCount => ActiveProcesses.Count;

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        networkCaptureService.TrafficObserved -= OnTrafficObserved;
        flushTimer.Stop();
    }

    public void ClearOnDisconnect()
    {
        lock (pendingLock)
        {
            pendingDownloadBytes = 0;
            pendingUploadBytes = 0;
            pendingByProcess.Clear();
        }

        latestDownloadMbps = 0;
        latestUploadMbps = 0;

        dlValues.Clear();
        ulValues.Clear();
        tickCount = 0;
        GraphXAxes[0].MinLimit = 0;
        GraphXAxes[0].MaxLimit = WindowSize;

        ActiveProcesses.Clear();
        processIndex.Clear();
        OnPropertyChanged(nameof(ProcessCount));
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
    }

    private void OnTrafficObserved(object? sender, NetworkTrafficEventArgs e)
    {
        lock (pendingLock)
        {
            if (!pendingByProcess.TryGetValue(e.ProcessName, out var pending))
            {
                pending = new PendingTraffic();
                pendingByProcess[e.ProcessName] = pending;
            }

            if (e.IsReceive)
            {
                pending.DownloadBytes += e.Bytes;
                pendingDownloadBytes += e.Bytes;
            }
            else
            {
                pending.UploadBytes += e.Bytes;
                pendingUploadBytes += e.Bytes;
            }
        }
    }

    private void FlushPendingTraffic()
    {
        Dictionary<string, PendingTraffic> pendingSnapshot;
        long secondDownloadBytes;
        long secondUploadBytes;

        lock (pendingLock)
        {
            secondDownloadBytes = pendingDownloadBytes;
            secondUploadBytes = pendingUploadBytes;
            pendingDownloadBytes = 0;
            pendingUploadBytes = 0;

            pendingSnapshot = new Dictionary<string, PendingTraffic>(pendingByProcess, StringComparer.OrdinalIgnoreCase);
            pendingByProcess.Clear();
        }

        latestDownloadMbps = secondDownloadBytes * 8d / 1_000_000d;
        latestUploadMbps = secondUploadBytes * 8d / 1_000_000d;
        currentSessionDownload += secondDownloadBytes;
        currentSessionUpload += secondUploadBytes;
        totalFromDateDownload += secondDownloadBytes;
        totalFromDateUpload += secondUploadBytes;

        AppendGraphPoint();
        ApplyProcessTick(pendingSnapshot);

        OnPropertyChanged(nameof(CurrentSessionDownloadText));
        OnPropertyChanged(nameof(CurrentSessionUploadText));
        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
    }

    private void AppendGraphPoint()
    {
        dlValues.Add(new ObservablePoint(tickCount, latestDownloadMbps));
        ulValues.Add(new ObservablePoint(tickCount, latestUploadMbps));

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
    }

    private void ApplyProcessTick(Dictionary<string, PendingTraffic> pendingSnapshot)
    {
        var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in pendingSnapshot)
        {
            touched.Add(kvp.Key);

            if (!processIndex.TryGetValue(kvp.Key, out var row))
            {
                row = new SummaryProcessRowViewModel(kvp.Key);
                processIndex[kvp.Key] = row;
                ActiveProcesses.Add(row);
                OnPropertyChanged(nameof(ProcessCount));
            }

            row.ApplyTick(kvp.Value.DownloadBytes, kvp.Value.UploadBytes);
        }

        foreach (var kvp in processIndex)
        {
            if (!touched.Contains(kvp.Key))
                kvp.Value.ResetCurrent();
        }
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
            "CurrentDownload" => p => p.CurrentDownloadBytes,
            "CurrentUpload" => p => p.CurrentUploadBytes,
            "TotalDownload" => p => p.TotalDownloadBytes,
            "TotalUpload" => p => p.TotalUploadBytes,
            _ => p => p.ProcessName
        };

        var sorted = sortDescending
            ? ActiveProcesses.OrderByDescending(selector).ToList()
            : ActiveProcesses.OrderBy(selector).ToList();

        ActiveProcesses.Clear();
        foreach (var item in sorted)
            ActiveProcesses.Add(item);
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

    private sealed class PendingTraffic
    {
        public long DownloadBytes;
        public long UploadBytes;
    }
}

public sealed class SummaryProcessRowViewModel : INotifyPropertyChanged
{
    private long currentDownloadBytes;
    private long currentUploadBytes;
    private long totalDownloadBytes;
    private long totalUploadBytes;

    public SummaryProcessRowViewModel(string processName)
    {
        ProcessName = processName;
    }

    public object? Icon => null;
    public string ProcessName { get; }
    public long CurrentDownloadBytes => currentDownloadBytes;
    public long CurrentUploadBytes => currentUploadBytes;
    public long TotalDownloadBytes => totalDownloadBytes;
    public long TotalUploadBytes => totalUploadBytes;

    public string CurrentDownload => FormatBytes(currentDownloadBytes);
    public string CurrentUpload => FormatBytes(currentUploadBytes);
    public string TotalDownload => FormatBytes(totalDownloadBytes);
    public string TotalUpload => FormatBytes(totalUploadBytes);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyTick(long secondDownloadBytes, long secondUploadBytes)
    {
        currentDownloadBytes = secondDownloadBytes;
        currentUploadBytes = secondUploadBytes;
        totalDownloadBytes += secondDownloadBytes;
        totalUploadBytes += secondUploadBytes;

        OnPropertyChanged(nameof(CurrentDownloadBytes));
        OnPropertyChanged(nameof(CurrentUploadBytes));
        OnPropertyChanged(nameof(TotalDownloadBytes));
        OnPropertyChanged(nameof(TotalUploadBytes));
        OnPropertyChanged(nameof(CurrentDownload));
        OnPropertyChanged(nameof(CurrentUpload));
        OnPropertyChanged(nameof(TotalDownload));
        OnPropertyChanged(nameof(TotalUpload));
    }

    public void ResetCurrent()
    {
        if (currentDownloadBytes == 0 && currentUploadBytes == 0)
            return;

        currentDownloadBytes = 0;
        currentUploadBytes = 0;
        OnPropertyChanged(nameof(CurrentDownloadBytes));
        OnPropertyChanged(nameof(CurrentUploadBytes));
        OnPropertyChanged(nameof(CurrentDownload));
        OnPropertyChanged(nameof(CurrentUpload));
    }

    public void ApplyTraffic(long bytes, bool isReceive)
    {
        if (isReceive) // kept for compatibility with older call sites
        {
            currentDownloadBytes = bytes;
            totalDownloadBytes += bytes;
            OnPropertyChanged(nameof(CurrentDownloadBytes));
            OnPropertyChanged(nameof(TotalDownloadBytes));
            OnPropertyChanged(nameof(CurrentDownload));
            OnPropertyChanged(nameof(TotalDownload));
        }
        else
        {
            currentUploadBytes = bytes;
            totalUploadBytes += bytes;
            OnPropertyChanged(nameof(CurrentUploadBytes));
            OnPropertyChanged(nameof(TotalUploadBytes));
            OnPropertyChanged(nameof(CurrentUpload));
            OnPropertyChanged(nameof(TotalUpload));
        }
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

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
