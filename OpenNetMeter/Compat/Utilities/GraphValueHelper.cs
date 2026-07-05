using System;

namespace OpenNetMeter.Utilities;

public static class GraphValueHelper
{
    private const double LogBase = 10d;
    private const int MaxMagnitude = 6;

    public static double MbpsToGraphValue(double mbps)
    {
        if (mbps <= 0)
            return 0;
        return Math.Log(mbps + 1, LogBase);
    }

    public static double GraphValueToMbps(double graphValue)
    {
        if (graphValue <= 0)
            return 0;
        return Math.Pow(LogBase, graphValue) - 1;
    }

    public static long GraphValueToBytesPerSecond(double graphValue)
    {
        if (graphValue <= 0)
            return 0;
        return (long)Math.Round(GraphValueToMbps(graphValue) * 1_000_000d / 8d);
    }

    public static decimal ScaleToMagnitude(long value, int magnitude)
    {
        var clampedMagnitude = Math.Clamp(magnitude, 0, MaxMagnitude);
        return (decimal)value / (1L << (clampedMagnitude * 10));
    }

    public static string FormatGraphAxisValue(decimal adjustedSize)
    {
        if (adjustedSize <= 0)
            return "0";

        var rounded = adjustedSize < 10
            ? decimal.Round(adjustedSize, 1)
            : decimal.Round(adjustedSize, 0);

        return rounded == decimal.Truncate(rounded)
            ? rounded.ToString("0")
            : rounded.ToString("0.#");
    }

    public static SpeedMagnitude NormalizeMagnitude(int magnitude)
    {
        return Enum.IsDefined(typeof(SpeedMagnitude), magnitude)
            ? (SpeedMagnitude)magnitude
            : SpeedMagnitude.Auto;
    }

    public static (decimal adjustedSize, int mag) GetAdjustedSize(long value, SpeedMagnitude magnitude)
    {
        int mag;
        decimal adjustedSize;

        if (magnitude == SpeedMagnitude.Auto)
        {
            mag = value > 0 ? (int)Math.Log(value, 1024) : 0;
            mag = Math.Clamp(mag, 0, 6);

            adjustedSize = (decimal)value / (1L << (mag * 10));
            if (Math.Round(adjustedSize, 1) >= 1000 && mag < 6)
            {
                mag += 1;
                adjustedSize /= 1024;
            }
        }
        else
        {
            mag = Math.Clamp((int)magnitude, 0, 6);
            adjustedSize = (decimal)value / (1L << (mag * 10));
        }

        return (adjustedSize, mag);
    }

    public static string BytesSuffix(int value)
    {
        return value switch
        {
            0 => "B",
            1 => "KB",
            2 => "MB",
            3 => "GB",
            4 => "TB",
            5 => "PB",
            6 => "EB",
            _ => "B"
        };
    }

    public static string BitsSuffix(int value)
    {
        return value switch
        {
            0 => "b",
            1 => "Kb",
            2 => "Mb",
            3 => "Gb",
            4 => "Tb",
            5 => "Pb",
            6 => "Eb",
            _ => "b"
        };
    }
}

public enum SpeedMagnitude
{
    Auto = 0,
    Kilo = 1,
    Mega = 2,
    Giga = 3
}
