using OpenNetMeter.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OpenNetMeter.ViewModels
{
    public class UnitConverterBits : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DataSizeSuffix.SizeSuffixInStr((ulong)value, 1, false);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
