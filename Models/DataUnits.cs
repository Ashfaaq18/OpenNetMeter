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
            (decimal, int) temp = DataSizeSuffix.SizeSuffix(value);
            DataValue = temp.Item1;
            DataSuffix = temp.Item2;
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
