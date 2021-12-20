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
        private ulong currentSessionDownloadData;
        private ulong currentSessionUploadData;
        public ulong CurrentSessionDownloadData
        {
            get { return currentSessionDownloadData; }
            set { currentSessionDownloadData = value; OnPropertyChanged("CurrentSessionDownloadData"); }
        }
        public ulong CurrentSessionUploadData
        {
            get { return currentSessionUploadData; }
            set { currentSessionUploadData = value; OnPropertyChanged("CurrentSessionUploadData"); }
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
