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
        public decimal dataValueBytes;
        public decimal DataValueBytes {
            get { return dataValueBytes; }
            set { dataValueBytes = value; OnPropertyChanged("DataValueBytes"); }
        }
        
        public decimal dataValueBits;
        public decimal DataValueBits {
            get { return dataValueBits; }
            set { dataValueBits = value; OnPropertyChanged("DataValueBits"); }
        }

        public int dataSuffix;
        public int DataSuffix {
            get { return dataSuffix; }
            set { dataSuffix = value; OnPropertyChanged("DataSuffix"); }
        }
        public DataUnits()
        {
            DataValueBytes = 0;
            DataValueBits = 0;
            DataSuffix = 0;
        }

        public void Conv_Bytes(ulong value)
        {
            (decimal, int) temp = DataSizeSuffix.SizeSuffix(value);
            DataValueBytes = temp.Item1;
            DataSuffix = temp.Item2;
        }
        
        public void Conv_Bits(ulong value)
        {
            (decimal, int) temp = DataSizeSuffix.SizeSuffix(value*8);
            DataValueBits = temp.Item1;
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
