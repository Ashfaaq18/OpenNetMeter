using System;
using System.Globalization;
using System.Windows.Data;

namespace WhereIsMyData.ViewModels
{
    public class UnitConverterBits : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value == 4 ? "Tb" : (int)value == 3 ? "Gb" : (int)value == 2 ? "Mb" : (int)value == 1 ? "Kb" : (int)value == 0 ? "b" : "Error";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
