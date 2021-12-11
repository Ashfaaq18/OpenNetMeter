using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    class DataUsageSummaryVM : INotifyPropertyChanged
    {
        private int seconds;
        public int Seconds
        {
            get { return seconds; }
            set { seconds = value; OnPropertyChanged("Seconds"); }
        }
        public DataUsageSummaryVM()
        {
            
        }

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
