using System;

namespace OpenNetMeter.Utilities
{
    internal static class DataSizeSuffix
    {
        //Bytes = false will give the suffix in bits. Size passed to this should aready be multiplied by 8 for bits
        internal static string InStr(long value, int decimalPlaces = 1, bool bytes = true)
        {
            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag;
            if (value > 0)
                mag = (int)Math.Log(value, 1024);
            else
                mag = (int)Math.Log(1, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << mag * 10);

            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            if (bytes)
                return decimal.Round(adjustedSize, 2).ToString() + InBytes(mag);
            else
                return decimal.Round(adjustedSize, 2).ToString() + InBits(mag);
        }

        internal static (double, int) InInt(long value, int decimalPlaces = 1)
        {
            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag;
            if (value > 0)
                mag = (int)Math.Log(value, 1024);
            else
                mag = (int)Math.Log(1, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << mag * 10);

            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return ((double)decimal.Round(adjustedSize, 2), mag);
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
