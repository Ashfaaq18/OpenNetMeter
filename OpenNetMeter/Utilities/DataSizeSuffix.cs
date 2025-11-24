using System;

namespace OpenNetMeter.Utilities
{
    public enum SpeedMagnitude
    {
        Auto = 0,
        Kilo = 1,
        Mega = 2,
        Giga = 3
    }

    internal static class DataSizeSuffix
    {
        //Bytes = false will give the suffix in bits. Size passed to this should aready be multiplied by 8 for bits
        internal static string InStr(long value, int decimalPlaces = 1, bool bytes = true, SpeedMagnitude magnitude = SpeedMagnitude.Auto)
        {
            (decimal adjustedSize, int mag) = GetAdjustedSize(value, decimalPlaces, magnitude);

            if (bytes)
                return decimal.Round(adjustedSize, 2).ToString() + InBytes(mag);
            else
                return decimal.Round(adjustedSize, 2).ToString() + InBits(mag);
        }

        internal static (double, int) InInt(long value, int decimalPlaces = 1, SpeedMagnitude magnitude = SpeedMagnitude.Auto)
        {
            (decimal adjustedSize, int mag) = GetAdjustedSize(value, decimalPlaces, magnitude);

            return ((double)decimal.Round(adjustedSize, 2), mag);
        }

        internal static SpeedMagnitude NormalizeMagnitude(int magnitude)
        {
            return Enum.IsDefined(typeof(SpeedMagnitude), magnitude) ? (SpeedMagnitude)magnitude : SpeedMagnitude.Auto;
        }

        private static (decimal adjustedSize, int mag) GetAdjustedSize(long value, int decimalPlaces, SpeedMagnitude magnitude)
        {
            int mag;
            decimal adjustedSize;

            if (magnitude == SpeedMagnitude.Auto)
            {
                // mag is 0 for bytes, 1 for KB, 2 for MB, etc.
                mag = value > 0 ? (int)Math.Log(value, 1024) : 0;
                if (mag < 0)
                    mag = 0;

                // 1L << (mag * 10) == 2 ^ (10 * mag)
                adjustedSize = (decimal)value / (1L << mag * 10);

                if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
                {
                    mag += 1;
                    adjustedSize /= 1024;
                }
            }
            else
            {
                mag = (int)magnitude;
                adjustedSize = (decimal)value / (1L << mag * 10);
            }

            return (adjustedSize, mag);
        }

        private static string InBytes(int value)
        {
            return value == 6 ? "EB" : value == 5 ? "PB" : value == 4 ? "TB" : value == 3 ? "GB" : value == 2 ? "MB" : value == 1 ? "KB" : value == 0 ? "B" : "Error";
        }

        private static string InBits(int value)
        {
            return value == 6 ? "Eb" : value == 5 ? "Pb" : value == 4 ? "Tb" : value == 3 ? "Gb" : value == 2 ? "Mb" : value == 1 ? "Kb" : value == 0 ? "b" : "Error";
        }
    }
}
