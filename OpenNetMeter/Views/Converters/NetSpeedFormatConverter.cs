using OpenNetMeter.Utilities;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OpenNetMeter.Views
{
    public class NetSpeedFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(Properties.Settings.Default.NetworkSpeedFormat == 0)
                return DataSizeSuffix.InStr((long)value, 1, false);
            else
                return DataSizeSuffix.InStr((long)value, 1, true);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
