using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Data;
using System.Windows.Input;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
        private decimal totalDownloadData;
        private int suffixOfTotalDownloadData;
        private decimal totalUploadData;
        private int suffixOfTotalUploadData;
        public decimal TotalDownloadData
        {
            get { return totalDownloadData; }
            set
            {
                totalDownloadData = value; OnPropertyChanged("TotalDownloadData");
            }
        }

        public int SuffixOfTotalDownloadData
        {
            get { return suffixOfTotalDownloadData; }
            set
            {
                suffixOfTotalDownloadData = value; OnPropertyChanged("suffixOfTotalDownloadData");
            }
        }

        public decimal TotalUploadData
        {
            get { return totalUploadData; }
            set
            {
                totalUploadData = value; OnPropertyChanged("TotalUploadData");
            }
        }

        public int SuffixOfTotalUploadData
        {
            get { return suffixOfTotalUploadData; }
            set
            {
                suffixOfTotalUploadData = value; OnPropertyChanged("SuffixOfTotalUploadData");
            }
        }

        private decimal currentSessionDownloadData;
        private int suffixOfDownloadData;
        private decimal currentSessionUploadData;
        private int suffixOfUploadData;
        public decimal CurrentSessionDownloadData
        {
            get { return currentSessionDownloadData; }
            set
            {
                currentSessionDownloadData = value; OnPropertyChanged("CurrentSessionDownloadData");
            }
        }
        public int SuffixOfDownloadData
        {
            get { return suffixOfDownloadData; }
            set
            {
                suffixOfDownloadData = value; OnPropertyChanged("SuffixOfDownloadData");
            }
        }

        public decimal CurrentSessionUploadData
        {
            get { return currentSessionUploadData; }
            set
            {
                currentSessionUploadData = value; OnPropertyChanged("CurrentSessionUploadData");
            }
        }
        public int SuffixOfUploadData
        {
            get { return suffixOfUploadData; }
            set
            {
                suffixOfUploadData = value; OnPropertyChanged("SuffixOfUploadData");
            }
        }

        public DataUsageSummaryVM()
        {
            currentSessionDownloadData = 0;
            currentSessionUploadData = 0;
            totalDownloadData = 0;
            totalUploadData = 0;
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
