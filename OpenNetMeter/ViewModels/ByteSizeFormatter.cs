namespace OpenNetMeter.ViewModels;

internal static class ByteSizeFormatter
{
    public static string FormatBytes(long value)
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
}

