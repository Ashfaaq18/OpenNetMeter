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
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
