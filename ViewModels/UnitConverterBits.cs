using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WhereIsMyData.ViewModels
{
    public class UnitConverterBits : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag;
            if ((ulong)value > 0)
                mag = (int)Math.Log((ulong)value, 1024);
            else
                mag = (int)Math.Log(1, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)(ulong)value / (1L << (mag * 10));

            if (Math.Round(adjustedSize, 1) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return Decimal.Round(adjustedSize, 2).ToString() + SuffixBits(mag);
        }
        private string SuffixBits(int value)
        {
            return value == 4 ? "Tbps" : value == 3 ? "Gbps" : value == 2 ? "Mbps" : value == 1 ? "Kbps" : value == 0 ? "bps" : "Error";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
