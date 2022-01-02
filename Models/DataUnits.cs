using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhereIsMyData.Models
{
    public class DataUnits : INotifyPropertyChanged
    {
        public decimal dataValue;
        public decimal DataValue {
            get { return dataValue; }
            set { dataValue = value; OnPropertyChanged("DataValue"); }
        }

        public int dataSuffix;
        public int DataSuffix {
            get { return dataSuffix; }
            set { dataSuffix = value; OnPropertyChanged("DataSuffix"); }
        }
        public DataUnits()
        {
            DataValue = 0;
            DataSuffix = 0;
        }

        public void Conv(ulong value)
        {
            (decimal, int) temp = SizeSuffix(value);
            DataValue = temp.Item1;
            DataSuffix = temp.Item2;
        }

        private (decimal, int) SizeSuffix(ulong value, int decimalPlaces = 1)
        {
            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag;
            if (value > 0)
                mag = (int)Math.Log(value, 1024);
            else
                mag = (int)Math.Log(1, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return (Decimal.Round(adjustedSize, 2), mag);
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
