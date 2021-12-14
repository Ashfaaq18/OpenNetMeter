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
    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
        private long currentSessionData;
        public long CurrentSessionData
        {
            get { return currentSessionData; }
            set { currentSessionData = value; OnPropertyChanged("CurrentSessionData"); }
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
