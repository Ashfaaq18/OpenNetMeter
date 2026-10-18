using System;

namespace OpenNetMeter.Models;

/// <summary>
/// How long daily usage history is kept in the database.
/// Stored in settings as a day count; <see cref="Forever"/> (0) disables the purge.
/// </summary>
internal static class HistoryRetention
{
    public const int Forever = 0;
    public const int DefaultDays = 60;

    public static readonly (string Label, int Days)[] Options =
    [
        ("60 days", 60),
        ("180 days", 180),
        ("1 year", 365),
        ("Forever", Forever)
    ];

    /// <summary>
    /// Returns the oldest date to keep, or null when nothing should be deleted.
    /// Negative values are treated as the default.
    /// </summary>
    public static DateTime? GetCutoff(DateTime today, int days)
    {
        if (days == Forever)
            return null;

        if (days < 0)
            days = DefaultDays;

        return today.Date.AddDays(-days);
    }

    /// <summary>
    /// Snaps a stored day count (e.g. hand-edited in settings.json) onto one of <see cref="Options"/>.
    /// Rounds up so normalizing never deletes more history than the stored value would.
    /// </summary>
    public static int Normalize(int days)
    {
        if (days == Forever)
            return Forever;

        if (days < 0)
            return DefaultDays;

        foreach (var (_, optionDays) in Options)
        {
            if (optionDays != Forever && optionDays >= days)
                return optionDays;
        }

        return Forever;
    }

    /// <summary>
    /// Maps a stored day count to an index in <see cref="Options"/>; unknown values map to the default.
    /// </summary>
    public static int IndexOf(int days)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            if (Options[i].Days == days)
                return i;
        }

        return IndexOf(DefaultDays);
    }

    /// <summary>
    /// True when switching from <paramref name="oldDays"/> to <paramref name="newDays"/> would delete data.
    /// </summary>
    public static bool IsShorter(int newDays, int oldDays)
    {
        if (newDays == Forever)
            return false;

        if (oldDays == Forever)
            return true;

        return newDays < oldDays;
    }
}
