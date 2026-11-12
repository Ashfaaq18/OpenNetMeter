using System;
using System.Globalization;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels;

/// <summary>
/// The mini widget's total: the current session, or usage since a date chosen independently of the
/// Summary page. The date is stored as <c>yyyy-MM-dd</c>, or as <see cref="FollowToday"/> when the user
/// picked today, so it moves to the new day at midnight and on later launches instead of freezing.
/// </summary>
internal static class MiniWidgetUsageFrom
{
    public const int ModeSession = 0;
    public const int ModeUsageFrom = 1;

    public const string FollowToday = "";
    private const string StorageFormat = "yyyy-MM-dd";

    public static int NormalizeMode(int mode) => mode == ModeUsageFrom ? ModeUsageFrom : ModeSession;

    /// <summary>
    /// Resolves a stored value to a date between the retention cutoff (if any) and <paramref name="today"/>.
    /// Empty, unparsable or future values mean today.
    /// </summary>
    public static DateTime Resolve(string? stored, DateTime today, int retentionDays)
    {
        today = today.Date;
        if (string.IsNullOrWhiteSpace(stored) ||
            !DateTime.TryParseExact(stored, StorageFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return today;
        }

        return Clamp(date, today, retentionDays);
    }

    /// <summary>Clamps a chosen date to the retention window and to <paramref name="today"/>.</summary>
    public static DateTime Clamp(DateTime date, DateTime today, int retentionDays)
    {
        today = today.Date;
        date = date.Date;
        if (date > today)
            return today;

        var cutoff = HistoryRetention.GetCutoff(today, retentionDays);
        if (cutoff is { } min && date < min)
            return min;

        return date;
    }

    /// <summary>Value to store for a chosen date: <see cref="FollowToday"/> when it is today.</summary>
    public static string ToStored(DateTime date, DateTime today) =>
        date.Date >= today.Date ? FollowToday : date.ToString(StorageFormat, CultureInfo.InvariantCulture);

    public static bool IsFollowToday(string? stored) => string.IsNullOrWhiteSpace(stored);
}
