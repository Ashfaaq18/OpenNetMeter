using OpenNetMeter.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OpenNetMeter.ViewModels
{
    public class UnitConverterBytes : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DataSizeSuffix.SizeSuffix((ulong)value, 1, true);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
