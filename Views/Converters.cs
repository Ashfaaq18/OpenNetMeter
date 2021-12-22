using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace WhereIsMyData.Views
{
    public class Converters : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((int)value == 4) { return "TB"; }
            else if ((int)value == 3) { return "GB"; }
            else if ((int)value == 2) { return "MB"; }
            else if ((int)value == 1) { return "KB"; }
            else if ((int)value == 0) { return "B"; }
            else { return "Error"; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
     
}
