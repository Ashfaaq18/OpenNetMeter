using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

/// <summary>
/// Backs the summary "Weekly Trend" card: one stacked bar per day for the last seven days
/// plus a week-over-week comparison against the seven days before those.
/// </summary>
public sealed class WeeklyUsageTrendViewModel : INotifyPropertyChanged
{
    public const int DayCount = 7;

    private long weekDownload;
    private long weekUpload;
    private long previousWeekTotal;

    public WeeklyUsageTrendViewModel()
    {
        Days = [];
        for (var i = 0; i < DayCount; i++)
            Days.Add(new WeeklyUsageDayViewModel());

        Reset();
    }

    public ObservableCollection<WeeklyUsageDayViewModel> Days { get; }

    public string TotalText => ByteSizeFormatter.FormatBytes(weekDownload + weekUpload);
    public string DownloadText => ByteSizeFormatter.FormatBytes(weekDownload);
    public string UploadText => ByteSizeFormatter.FormatBytes(weekUpload);

    /// <summary>The delta badge stays hidden until there is a prior week to compare against.</summary>
    public bool HasComparison => previousWeekTotal > 0;
    public bool IsIncrease => weekDownload + weekUpload >= previousWeekTotal;

    public string DeltaText
    {
        get
        {
            if (previousWeekTotal <= 0)
                return string.Empty;

            var percent = (weekDownload + weekUpload - previousWeekTotal) * 100.0 / previousWeekTotal;
            return Math.Abs(percent).ToString("0", CultureInfo.CurrentCulture) + "%";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Reset()
    {
        weekDownload = 0;
        weekUpload = 0;
        previousWeekTotal = 0;

        ApplyDays(DateTime.Today, new Dictionary<int, (long download, long upload)>());
        NotifyTotalsChanged();
    }

    public void Refresh(string? adapterName)
    {
        if (string.IsNullOrWhiteSpace(adapterName))
        {
            Reset();
            return;
        }

        var today = DateTime.Today;
        var dailyTotals = ReadDailyTotals(adapterName, today.AddDays(-((DayCount * 2) - 1)), today);

        weekDownload = 0;
        weekUpload = 0;
        previousWeekTotal = 0;

        for (var offset = 0; offset < DayCount; offset++)
        {
            if (dailyTotals.TryGetValue(UsageDatabase.ToDateInt(today.AddDays(-offset)), out var current))
            {
                weekDownload += current.download;
                weekUpload += current.upload;
            }

            if (dailyTotals.TryGetValue(UsageDatabase.ToDateInt(today.AddDays(-(offset + DayCount))), out var previous))
                previousWeekTotal += previous.download + previous.upload;
        }

        ApplyDays(today, dailyTotals);
        NotifyTotalsChanged();
    }

    /// <summary>
    /// Fills the seven bars oldest-first, scaling each one against the busiest day of the window
    /// so the card reads as a trend rather than as absolute volume.
    /// </summary>
    private void ApplyDays(DateTime today, Dictionary<int, (long download, long upload)> dailyTotals)
    {
        var peak = 0L;
        for (var i = 0; i < DayCount; i++)
        {
            dailyTotals.TryGetValue(UsageDatabase.ToDateInt(DateForSlot(today, i)), out var totals);
            peak = Math.Max(peak, totals.download + totals.upload);
        }

        for (var i = 0; i < DayCount; i++)
        {
            var date = DateForSlot(today, i);
            dailyTotals.TryGetValue(UsageDatabase.ToDateInt(date), out var totals);
            Days[i].Update(date, totals.download, totals.upload, peak, date == today);
        }
    }

    private static DateTime DateForSlot(DateTime today, int slot) => today.AddDays(slot - (DayCount - 1));

    private static Dictionary<int, (long download, long upload)> ReadDailyTotals(string adapterName, DateTime startDate, DateTime endDate)
    {
        var totals = new Dictionary<int, (long download, long upload)>();

        try
        {
            var dbPath = UsageDatabase.ResolveDatabasePath();
            if (!File.Exists(dbPath))
                return totals;

            using var connection = UsageDatabase.OpenReadOnlyConnection(dbPath);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT d.Year, d.Month, d.Day, SUM(pd.DataReceived) AS TotalRecv, SUM(pd.DataSent) AS TotalSent " +
                "FROM ProcessDate pd " +
                "JOIN Adapter a ON a.ID = pd.AdapterID " +
                "JOIN Date d ON d.ID = pd.DateID " +
                "WHERE a.Name = @AdapterName " +
                "AND (d.Year * 10000 + d.Month * 100 + d.Day) BETWEEN @StartDate AND @EndDate " +
                "GROUP BY d.Year, d.Month, d.Day";
            command.Parameters.AddWithValue("@AdapterName", adapterName);
            command.Parameters.AddWithValue("@StartDate", UsageDatabase.ToDateInt(startDate));
            command.Parameters.AddWithValue("@EndDate", UsageDatabase.ToDateInt(endDate));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var dateInt = (reader.GetInt32(0) * 10000) + (reader.GetInt32(1) * 100) + reader.GetInt32(2);
                var download = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                var upload = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                totals[dateInt] = (download, upload);
            }
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to read weekly usage trend from database for adapter '{adapterName}'", ex);
        }

        return totals;
    }

    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(UploadText));
        OnPropertyChanged(nameof(HasComparison));
        OnPropertyChanged(nameof(IsIncrease));
        OnPropertyChanged(nameof(DeltaText));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// A single day column of the weekly trend card. Bar heights are pre-scaled to pixels here so
/// the view stays a plain bottom-aligned stack of two coloured borders.
/// </summary>
public sealed class WeeklyUsageDayViewModel : INotifyPropertyChanged
{
    /// <summary>Height of the bar track; kept in sync with the bar row height in Summary.axaml.</summary>
    private const double BarAreaHeight = 34;
    private const double MinBarHeight = 3;

    private string dayLabel = string.Empty;
    private string tooltipText = string.Empty;
    private bool isToday;
    private double downloadBarHeight;
    private double uploadBarHeight;

    public string DayLabel => dayLabel;
    public string TooltipText => tooltipText;
    public bool IsToday => isToday;
    public double DownloadBarHeight => downloadBarHeight;
    public double UploadBarHeight => uploadBarHeight;
    public double TotalBarHeight => downloadBarHeight + uploadBarHeight;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(DateTime date, long downloadBytes, long uploadBytes, long peakBytes, bool isDateToday)
    {
        var total = downloadBytes + uploadBytes;
        double downloadHeight = 0;
        double uploadHeight = 0;

        if (total > 0)
        {
            var scaled = Math.Clamp(BarAreaHeight * total / Math.Max(peakBytes, total), MinBarHeight, BarAreaHeight);

            // Give each direction at least a hairline so a lopsided day still shows both colours.
            downloadHeight = downloadBytes == 0 ? 0 : Math.Max(1, Math.Round(scaled * downloadBytes / total));
            uploadHeight = uploadBytes == 0 ? 0 : Math.Max(1, scaled - downloadHeight);

            // Rounding and the hairline floor can push the stack past the scaled height.
            var overflow = downloadHeight + uploadHeight - scaled;
            if (overflow > 0)
            {
                if (downloadHeight >= uploadHeight)
                    downloadHeight = Math.Max(1, downloadHeight - overflow);
                else
                    uploadHeight = Math.Max(1, uploadHeight - overflow);
            }
        }

        dayLabel = CultureInfo.CurrentCulture.DateTimeFormat.GetShortestDayName(date.DayOfWeek);
        tooltipText = $"{date.ToString("ddd, MMM d", CultureInfo.CurrentCulture)}  •  DL {ByteSizeFormatter.FormatBytes(downloadBytes)}  •  UL {ByteSizeFormatter.FormatBytes(uploadBytes)}";
        isToday = isDateToday;
        downloadBarHeight = downloadHeight;
        uploadBarHeight = uploadHeight;

        OnPropertyChanged(nameof(DayLabel));
        OnPropertyChanged(nameof(TooltipText));
        OnPropertyChanged(nameof(IsToday));
        OnPropertyChanged(nameof(DownloadBarHeight));
        OnPropertyChanged(nameof(UploadBarHeight));
        OnPropertyChanged(nameof(TotalBarHeight));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
