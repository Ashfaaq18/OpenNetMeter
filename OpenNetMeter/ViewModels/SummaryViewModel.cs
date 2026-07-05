using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia.Threading;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Data.Sqlite;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;
using SkiaSharp;

namespace OpenNetMeter.ViewModels;

public sealed class SummaryViewModel : INotifyPropertyChanged, IDisposable
{
    private const int WindowSize = 35;
    private readonly INetworkCaptureService networkCaptureService;
    private readonly IProcessIconService processIconService;
    private readonly IExternalLinkService externalLinkService;
    private readonly ObservableCollection<ObservablePoint> dlValues = new();
    private readonly ObservableCollection<ObservablePoint> ulValues = new();
    private readonly Dictionary<string, SummaryProcessRowViewModel> processIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object pendingLock = new();
    private readonly Dictionary<string, PendingTraffic> pendingByProcess = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer flushTimer;
    private string? currentSortColumn;
    private bool sortDescending;
    private string activeAdapterName = string.Empty;
    private long sinceDateDbDownloadBaseline;
    private long sinceDateDbUploadBaseline;
    private long sinceDateSessionDownloadBaseline;
    private long sinceDateSessionUploadBaseline;

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
    private long latestDownloadBytesPerSecond;
    private long latestUploadBytesPerSecond;
    private readonly GraphAxisManager graphAxisManager = new();

    public SummaryViewModel(INetworkCaptureService networkCaptureService, IProcessIconService processIconService, IExternalLinkService externalLinkService)
    {
        this.networkCaptureService = networkCaptureService;
        this.processIconService = processIconService;
        this.externalLinkService = externalLinkService;

        // Match WPF dark theme accents:
        // Download -> #367061, Upload -> #D98868
        var dlColor = new SKColor(0x4A, 0xA9, 0x8C);
        var ulColor = new SKColor(0xE1, 0x77, 0x17);

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

        GraphYAxes = graphAxisManager.CreateYAxes();

        ActiveProcesses = [];
        SortProcessesCommand = new ParameterRelayCommand(parameter =>
        {
            var column = parameter?.ToString();
            if (string.IsNullOrWhiteSpace(column))
                return;

            SortProcesses(column);
        });

        DateMax = DateTime.Today;
        DateMin = DateMax.AddDays(-ApplicationDB.DataStoragePeriodInDays);
        sinceDate = DateMax;
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
    public Axis[] GraphYAxes { get; private set; }
    public ObservableCollection<SummaryProcessRowViewModel> ActiveProcesses { get; }
    public ICommand SortProcessesCommand { get; }

    public string? CurrentSortColumn => currentSortColumn;
    public bool IsSortDescending => sortDescending;

    public string CurrentSessionDownloadText => ByteSizeFormatter.FormatBytes(currentSessionDownload);
    public string CurrentSessionUploadText => ByteSizeFormatter.FormatBytes(currentSessionUpload);
    public string TotalFromDateDownloadText => ByteSizeFormatter.FormatBytes(totalFromDateDownload);
    public string TotalFromDateUploadText => ByteSizeFormatter.FormatBytes(totalFromDateUpload);
    public string DownloadSpeedText => $"{FormatSpeed(latestDownloadBytesPerSecond)}ps";
    public string UploadSpeedText => $"{FormatSpeed(latestUploadBytesPerSecond)}ps";
    public int ProcessCount => ActiveProcesses.Count;
    public DateTime DateMin { get; }
    public DateTime DateMax { get; }

    public DateTimeOffset? SinceDate
    {
        get => sinceDate;
        set
        {
            var normalized = NormalizeSinceDate(value);
            if (sinceDate == normalized)
                return;

            sinceDate = normalized;
            RefreshSinceDateBaseline();
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
        latestDownloadBytesPerSecond = 0;
        latestUploadBytesPerSecond = 0;
        currentSessionDownload = 0;
        currentSessionUpload = 0;
        totalFromDateDownload = 0;
        totalFromDateUpload = 0;
        sinceDateDbDownloadBaseline = 0;
        sinceDateDbUploadBaseline = 0;
        sinceDateSessionDownloadBaseline = 0;
        sinceDateSessionUploadBaseline = 0;
        activeAdapterName = string.Empty;

        dlValues.Clear();
        ulValues.Clear();
        tickCount = 0;
        GraphXAxes[0].MinLimit = 0;
        GraphXAxes[0].MaxLimit = WindowSize;

        ActiveProcesses.Clear();
        processIndex.Clear();
        OnPropertyChanged(nameof(ProcessCount));
        graphAxisManager.UpdateScale(dlValues, ulValues, GraphYAxes);
        OnPropertyChanged(nameof(CurrentSessionDownloadText));
        OnPropertyChanged(nameof(CurrentSessionUploadText));
        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
    }

    public void SetActiveAdapter(string adapterName)
    {
        var normalized = adapterName?.Trim() ?? string.Empty;
        if (string.Equals(activeAdapterName, normalized, StringComparison.Ordinal))
            return;

        activeAdapterName = normalized;
        RefreshSinceDateBaseline();
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
        latestDownloadBytesPerSecond = secondDownloadBytes;
        latestUploadBytesPerSecond = secondUploadBytes;
        currentSessionDownload += secondDownloadBytes;
        currentSessionUpload += secondUploadBytes;
        UpdateTotalFromDateFromBaselines();

        AppendGraphPoint();
        ApplyProcessTick(pendingSnapshot);

        OnPropertyChanged(nameof(CurrentSessionDownloadText));
        OnPropertyChanged(nameof(CurrentSessionUploadText));
        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
    }

    private void RefreshSinceDateBaseline()
    {
        sinceDateSessionDownloadBaseline = currentSessionDownload;
        sinceDateSessionUploadBaseline = currentSessionUpload;

        if (string.IsNullOrWhiteSpace(activeAdapterName))
        {
            sinceDateDbDownloadBaseline = 0;
            sinceDateDbUploadBaseline = 0;
            totalFromDateDownload = 0;
            totalFromDateUpload = 0;
        }
        else
        {
            var fromDate = (sinceDate ?? DateTimeOffset.Now.Date).Date;
            var totals = ReadDbTotals(activeAdapterName, fromDate, DateTime.Today);
            sinceDateDbDownloadBaseline = totals.download;
            sinceDateDbUploadBaseline = totals.upload;
            UpdateTotalFromDateFromBaselines();
        }

        OnPropertyChanged(nameof(TotalFromDateDownloadText));
        OnPropertyChanged(nameof(TotalFromDateUploadText));
    }

    private void UpdateTotalFromDateFromBaselines()
    {
        var sessionDownloadDelta = currentSessionDownload - sinceDateSessionDownloadBaseline;
        var sessionUploadDelta = currentSessionUpload - sinceDateSessionUploadBaseline;

        if (sessionDownloadDelta < 0)
            sessionDownloadDelta = 0;
        if (sessionUploadDelta < 0)
            sessionUploadDelta = 0;

        totalFromDateDownload = sinceDateDbDownloadBaseline + sessionDownloadDelta;
        totalFromDateUpload = sinceDateDbUploadBaseline + sessionUploadDelta;
    }

    private static (long download, long upload) ReadDbTotals(string adapterName, DateTime startDate, DateTime endDate)
    {
        try
        {
            var dbPath = ResolveDatabasePath();
            if (!File.Exists(dbPath))
                return (0, 0);

            var fromDate = startDate.Date;
            var toDate = endDate.Date;
            if (toDate < fromDate)
                (fromDate, toDate) = (toDate, fromDate);

            using var connection = OpenReadOnlyConnection(dbPath);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
                "FROM ProcessDate pd " +
                "JOIN Adapter a ON a.ID = pd.AdapterID " +
                "JOIN Date d ON d.ID = pd.DateID " +
                "WHERE a.Name = @AdapterName " +
                "AND (d.Year * 10000 + d.Month * 100 + d.Day) BETWEEN @StartDate AND @EndDate";
            command.Parameters.AddWithValue("@AdapterName", adapterName);
            command.Parameters.AddWithValue("@StartDate", ToDateInt(fromDate));
            command.Parameters.AddWithValue("@EndDate", ToDateInt(toDate));

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var download = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                var upload = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                return (download, upload);
            }
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to read summary totals from database for adapter '{adapterName}'", ex);
        }

        return (0, 0);
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

    private DateTimeOffset NormalizeSinceDate(DateTimeOffset? value)
    {
        var candidate = (value ?? DateTimeOffset.Now.Date).Date;
        if (candidate > DateMax)
            candidate = DateMax;
        if (candidate < DateMin)
            candidate = DateMin;
        return candidate;
    }

    private void AppendGraphPoint()
    {
        dlValues.Add(new ObservablePoint(tickCount, GraphValueHelper.MbpsToGraphValue(latestDownloadMbps)));
        ulValues.Add(new ObservablePoint(tickCount, GraphValueHelper.MbpsToGraphValue(latestUploadMbps)));

        while (dlValues.Count > WindowSize)
            dlValues.RemoveAt(0);
        while (ulValues.Count > WindowSize)
            ulValues.RemoveAt(0);

        if (tickCount >= WindowSize)
        {
            GraphXAxes[0].MinLimit = tickCount - WindowSize;
            GraphXAxes[0].MaxLimit = tickCount;
        }

        graphAxisManager.UpdateScale(dlValues, ulValues, GraphYAxes);
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
                row = new SummaryProcessRowViewModel(kvp.Key, externalLinkService, processIconService.GetProcessIcon(kvp.Key) as IImage);
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

    private static readonly Dictionary<string, Func<SummaryProcessRowViewModel, IComparable>> SortSelectors = new(StringComparer.Ordinal)
    {
        ["Name"] = p => p.ProcessName,
        ["CurrentDownload"] = p => p.CurrentDownloadBytes,
        ["CurrentUpload"] = p => p.CurrentUploadBytes,
        ["TotalDownload"] = p => p.TotalDownloadBytes,
        ["TotalUpload"] = p => p.TotalUploadBytes
    };

    private void SortProcesses(string column)
    {
        sortDescending = string.Equals(currentSortColumn, column, StringComparison.Ordinal) ? !sortDescending : false;
        currentSortColumn = column;

        NotifySortPropertiesChanged();

        var selector = SortSelectors.GetValueOrDefault(column, SortSelectors["Name"]);
        var sorted = sortDescending
            ? ActiveProcesses.OrderByDescending(selector).ToList()
            : ActiveProcesses.OrderBy(selector).ToList();

        ActiveProcesses.Clear();
        foreach (var item in sorted)
            ActiveProcesses.Add(item);
    }

    private void NotifySortPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentSortColumn));
        OnPropertyChanged(nameof(IsSortDescending));
    }

    public void RefreshSpeedDisplayFormat()
    {
        graphAxisManager.UpdateScale(dlValues, ulValues, GraphYAxes);
        GraphYAxes = graphAxisManager.CreateYAxes();
        OnPropertyChanged(nameof(GraphYAxes));
        OnPropertyChanged(nameof(DownloadSpeedText));
        OnPropertyChanged(nameof(UploadSpeedText));
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        var useBytes = SettingsManager.Current.NetworkSpeedFormat != 0;
        var magnitude = GraphValueHelper.NormalizeMagnitude(SettingsManager.Current.NetworkSpeedMagnitude);
        var value = useBytes ? bytesPerSecond : bytesPerSecond * 8;
        var (adjustedSize, mag) = GraphValueHelper.GetAdjustedSize(value, magnitude);

        return decimal.Round(adjustedSize, 2).ToString() + (useBytes ? GraphValueHelper.BytesSuffix(mag) : GraphValueHelper.BitsSuffix(mag));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class PendingTraffic
    {
        public long DownloadBytes;
        public long UploadBytes;
    }
}

public sealed class SummaryProcessRowViewModel : INotifyPropertyChanged
{
    private readonly IExternalLinkService externalLinkService;
    private long currentDownloadBytes;
    private long currentUploadBytes;
    private long totalDownloadBytes;
    private long totalUploadBytes;

    public SummaryProcessRowViewModel(string processName, IExternalLinkService externalLinkService, IImage? icon = null)
    {
        ProcessName = processName;
        this.externalLinkService = externalLinkService;
        Icon = icon;
        SearchProcessCommand = new RelayCommand(() =>
        {
            var encodedName = Uri.EscapeDataString(ProcessName);
            externalLinkService.Open($"https://www.google.com/search?q={encodedName}");
        });
    }

    public IImage? Icon { get; }
    public string ProcessName { get; }
    public ICommand SearchProcessCommand { get; }
    public long CurrentDownloadBytes => currentDownloadBytes;
    public long CurrentUploadBytes => currentUploadBytes;
    public long TotalDownloadBytes => totalDownloadBytes;
    public long TotalUploadBytes => totalUploadBytes;

    public string CurrentDownload => ByteSizeFormatter.FormatBytes(currentDownloadBytes);
    public string CurrentUpload => ByteSizeFormatter.FormatBytes(currentUploadBytes);
    public string TotalDownload => ByteSizeFormatter.FormatBytes(totalDownloadBytes);
    public string TotalUpload => ByteSizeFormatter.FormatBytes(totalUploadBytes);

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

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

