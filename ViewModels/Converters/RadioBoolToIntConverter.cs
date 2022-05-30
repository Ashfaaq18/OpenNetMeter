using System;
using System.Globalization;
using System.Windows.Data;

namespace OpenNetMeter.ViewModels
{
    public class RadioBoolToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int integer = (int)value;
            if (parameter != null)
            {
                if (integer == int.Parse(parameter.ToString()!))
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
}
