using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Views
{
    public class UiVisibilityToWpfVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not UiVisibility uiVisibility)
                return Visibility.Hidden;

            return uiVisibility switch
            {
                UiVisibility.Visible => Visibility.Visible,
                UiVisibility.Collapsed => Visibility.Collapsed,
                _ => Visibility.Hidden,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not Visibility visibility)
                return UiVisibility.Hidden;

            return visibility switch
            {
                Visibility.Visible => UiVisibility.Visible,
                Visibility.Collapsed => UiVisibility.Collapsed,
                _ => UiVisibility.Hidden,
            };
        }
    }
}
