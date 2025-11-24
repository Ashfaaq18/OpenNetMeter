using OpenNetMeter.Properties;
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
            bool useBytes = SettingsManager.Current.NetworkSpeedFormat != 0;
            SpeedMagnitude magnitude = DataSizeSuffix.NormalizeMagnitude(SettingsManager.Current.NetworkSpeedMagnitude);

            return DataSizeSuffix.InStr((long)value, 1, useBytes, magnitude);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
