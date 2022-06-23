using OpenNetMeter.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OpenNetMeter.ViewModels
{
    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
        private long totalDownloadData;
        public long TotalDownloadData
        {
            get { return totalDownloadData; }
            set
            {
                totalDownloadData = value;
                OnPropertyChanged("TotalDownloadData");
            }
        }
        private long totalUploadData;
        public long TotalUploadData
        {
            get { return totalUploadData; }
            set
            {
                totalUploadData = value;
                OnPropertyChanged("TotalUploadData");
            }
        }

        private long currentSessionDownloadData;
        public long CurrentSessionDownloadData
        {
            get { return currentSessionDownloadData; }
            set
            {
                currentSessionDownloadData = value;
                OnPropertyChanged("CurrentSessionDownloadData");
            }
        }
        private long currentSessionUploadData;
        public long CurrentSessionUploadData
        {
            get { return currentSessionUploadData; }
            set
            {
                currentSessionUploadData = value;
                OnPropertyChanged("CurrentSessionUploadData");
            }
        }

        private string totalUsageText = "";
        public string TotalUsageText
        {
            get { return totalUsageText; }
            set
            {
                totalUsageText = value;
                OnPropertyChanged("TotalUsageText");
            }
        }

        public SpeedGraph Graph { get; set; }

        public DataUsageSummaryVM()
        {
            TotalDownloadData = 0;
            TotalUploadData = 0;
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            TotalUsageText = "Total data usage of the past 0 days";

            Graph = new SpeedGraph(7, 7);
            Graph.Init();
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
