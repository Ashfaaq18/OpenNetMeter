using System;
using System.Globalization;

namespace OpenNetMeter.Utilities;

/// <summary>
/// The date/time text shown in the UI and in exported reports, e.g. "22 Oct 2026" and "22 Oct 2026, 14:05".
/// </summary>
internal static class DisplayDateFormat
{
    public static string Date(DateTime value) =>
        value.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public static string DateTime(DateTime value) =>
        Date(value) + ", " + value.ToString("t", CultureInfo.CurrentCulture);
}
