using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels
{
    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
        private TrayPopupVM tpvm;
        public NetworkSpeedGraph SpeedGraph { get; set; }

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
                tpvm.CurrentSessionDownloadData = value;
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
                tpvm.CurrentSessionUploadData = value;
                OnPropertyChanged("CurrentSessionUploadData");
            }
        }

        private string totalUsageText;
        public string TotalUsageText
        {
            get { return totalUsageText; }
            set
            {
                totalUsageText = value; 
                OnPropertyChanged("TotalUsageText"); 
            }
        }

        public DataUsageSummaryVM(ref TrayPopupVM tpvm_ref)
        {
            tpvm = tpvm_ref;
            TotalDownloadData = 0;
            TotalUploadData = 0;
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            SpeedGraph = new NetworkSpeedGraph();
            TotalUsageText = "Total data usage of the past 0 days";
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
