using System;
using OpenNetMeter.Models;
using System.ComponentModel;

namespace OpenNetMeter.ViewModels
{
    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
        private long todayDownloadData;
        public long TodayDownloadData
        {
            get { return todayDownloadData; }
            set
            {
                todayDownloadData = value;
                OnPropertyChanged("TodayDownloadData");
            }
        }

        private long todayUploadData;
        public long TodayUploadData
        {
            get { return todayUploadData; }
            set
            {
                todayUploadData = value;
                OnPropertyChanged("TodayUploadData");
            }
        }

        private DateTime dateMin;
        public DateTime DateMin
        {
            get { return dateMin; }
            private set
            {
                if (dateMin != value)
                {
                    dateMin = value;
                    OnPropertyChanged("DateMin");
                }
            }
        }

        private DateTime dateMax;
        public DateTime DateMax
        {
            get { return dateMax; }
            private set
            {
                if (dateMax != value)
                {
                    dateMax = value;
                    OnPropertyChanged("DateMax");
                }
            }
        }

        private DateTime sinceDate;
        public DateTime SinceDate
        {
            get { return sinceDate; }
            set
            {
                DateTime newDate = value.Date;

                if (newDate > DateMax)
                    newDate = DateMax;
                else if (newDate < DateMin)
                    newDate = DateMin;

                if (sinceDate != newDate)
                {
                    sinceDate = newDate;
                    OnPropertyChanged("SinceDate");
                }
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

        public SpeedGraph Graph { get; set; }

        public DataUsageSummaryVM()
        {
            TodayDownloadData = 0;
            TodayUploadData = 0;
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;

            Graph = new SpeedGraph(7, 7);
            Graph.Init();

            RefreshDateBounds();
            SinceDate = DateTime.Today;
        }

        public void RefreshDateBounds()
        {
            DateMax = DateTime.Today;
            DateMin = DateTime.Today.AddDays(-1 * ApplicationDB.DataStoragePeriodInDays);

            if (sinceDate == default)
                sinceDate = DateMax;

            if (SinceDate > DateMax)
            {
                SinceDate = DateMax;
            }
            else if (SinceDate < DateMin)
            {
                SinceDate = DateMin;
            }
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
