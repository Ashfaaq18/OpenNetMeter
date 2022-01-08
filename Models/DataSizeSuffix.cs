using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhereIsMyData.Models
{
    public class DataSizeSuffix
    {
        public static string SizeSuffix(ulong value, int decimalPlaces = 1, bool Bytes = true)
        {
            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag;
            if (value > 0)
                mag = (int)Math.Log(value, 1024);
            else
                mag = (int)Math.Log(1, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            if(Bytes)
                return Decimal.Round(adjustedSize, 2).ToString() + SuffixBytes(mag);
            else
                return Decimal.Round(adjustedSize, 2).ToString() + SuffixBits(mag);
        }

        public static string SuffixBytes(int value)
        {
            return value == 4 ? "TB" : value == 3 ? "GB" : value == 2 ? "MB" : value == 1 ? "KB" : value == 0 ? "B" : "Error";
        }
        
        public static string SuffixBits(int value)
        {
            return value == 4 ? "Tb" : value == 3 ? "Gb" : value == 2 ? "Mb" : value == 1 ? "Kb" : value == 0 ? "b" : "Error";
        }
    }
}
