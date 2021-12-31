using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WhereIsMyData.ViewModels
{
    public class UnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)value == 4 ? "TB" : (int)value == 3 ? "GB" : (int)value == 2 ? "MB" : (int)value == 1 ? "KB" : (int)value == 0 ? "B" : "Error";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
