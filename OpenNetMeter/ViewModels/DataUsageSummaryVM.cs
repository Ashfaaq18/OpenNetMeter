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
        private ulong totalDownloadData;
        public ulong TotalDownloadData
        {
            get { return totalDownloadData; }
            set
            {
                totalDownloadData = value;
                OnPropertyChanged("TotalDownloadData");
            }
        }
        private ulong totalUploadData;
        public ulong TotalUploadData
        {
            get { return totalUploadData; }
            set
            {
                totalUploadData = value;
                OnPropertyChanged("TotalUploadData");
            }
        }

        private ulong currentSessionDownloadData;
        public ulong CurrentSessionDownloadData
        {
            get { return currentSessionDownloadData; }
            set
            {
                currentSessionDownloadData = value;
                OnPropertyChanged("CurrentSessionDownloadData");
            }
        }
        private ulong currentSessionUploadData;
        public ulong CurrentSessionUploadData
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
