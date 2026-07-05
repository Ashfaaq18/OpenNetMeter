using System;
using System.Collections.ObjectModel;
using System.Linq;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

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

public sealed class GraphAxisManager
{
    private int magnitude;

    public GraphAxisManager()
    {
        magnitude = 0;
    }

    public Axis[] CreateYAxes()
    {
        return
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 1,
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(new SKColor(0x55, 0x55, 0x55)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(new SKColor(0xA9, 0xAB, 0xAB)),
                TextSize = 10,
                Labeler = FormatLabel
            }
        ];
    }

    public void UpdateScale(
        ObservableCollection<ObservablePoint> dlValues,
        ObservableCollection<ObservablePoint> ulValues,
        Axis[] graphYAxes)
    {
        var useBytes = OpenNetMeter.Properties.SettingsManager.Current.NetworkSpeedFormat != 0;
        long maxBytesPerSecond = 0;
        double maxGraphValue = 0;

        if (dlValues.Count > 0)
        {
            var maxDl = dlValues.Max(point => point.Y ?? 0d);
            maxGraphValue = Math.Max(maxGraphValue, maxDl);
            maxBytesPerSecond = Math.Max(maxBytesPerSecond, GraphValueHelper.GraphValueToBytesPerSecond(maxDl));
        }

        if (ulValues.Count > 0)
        {
            var maxUl = ulValues.Max(point => point.Y ?? 0d);
            maxGraphValue = Math.Max(maxGraphValue, maxUl);
            maxBytesPerSecond = Math.Max(maxBytesPerSecond, GraphValueHelper.GraphValueToBytesPerSecond(maxUl));
        }

        if (maxGraphValue > 0 && graphYAxes.Length > 0)
        {
            var niceMax = Math.Ceiling(maxGraphValue * 2) / 2;
            if (niceMax < 1) niceMax = 1;
            graphYAxes[0].MaxLimit = niceMax;
        }

        var axisMax = graphYAxes.Length > 0 ? (graphYAxes[0].MaxLimit ?? 1) : 1;
        var axisMaxBytes = GraphValueHelper.GraphValueToBytesPerSecond(axisMax);
        var displayValue = useBytes ? axisMaxBytes : axisMaxBytes * 8;
        var (_, newMagnitude) = GraphValueHelper.GetAdjustedSize(displayValue, SpeedMagnitude.Auto);

        if (magnitude == newMagnitude)
            return;

        magnitude = newMagnitude;
    }

    public string FormatLabel(double graphValue)
    {
        var bytesPerSecond = GraphValueHelper.GraphValueToBytesPerSecond(graphValue);
        var useBytes = OpenNetMeter.Properties.SettingsManager.Current.NetworkSpeedFormat != 0;
        var displayValue = useBytes ? bytesPerSecond : bytesPerSecond * 8;
        var adjustedSize = GraphValueHelper.ScaleToMagnitude(displayValue, magnitude);
        var suffix = useBytes ? GraphValueHelper.BytesSuffix(magnitude) : GraphValueHelper.BitsSuffix(magnitude);

        return $"{GraphValueHelper.FormatGraphAxisValue(adjustedSize)} {suffix}/s";
    }
}

public enum SpeedMagnitude
{
    Auto = 0,
    Kilo = 1,
    Mega = 2,
    Giga = 3
}
