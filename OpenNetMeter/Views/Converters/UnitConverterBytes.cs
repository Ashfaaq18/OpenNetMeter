﻿using OpenNetMeter.Utilities;
using System;
using System.Globalization;
using System.Windows.Data;

namespace OpenNetMeter.Views
{
    public class UnitConverterBytes : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DataSizeSuffix.InStr((long)value, 1, true);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
