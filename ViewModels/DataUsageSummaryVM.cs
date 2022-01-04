using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
        public NetworkSpeedGraph SpeedGraph { get; set; }
        
        public DataUnits TotalDownloadData { get; set; }
        public DataUnits TotalUploadData { get; set; }
        public DataUnits CurrentSessionDownloadData { get; set; }
        public DataUnits CurrentSessionUploadData { get; set; }

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
        private string date;
        public string Date
        {
            get { return date; }
            set
            {
                date = value; OnPropertyChanged("Date");
            }
        }

        public DataUsageSummaryVM()
        {
            //UploadSpeed = new DataUnits();
            TotalDownloadData = new DataUnits();
            TotalUploadData = new DataUnits();
            CurrentSessionDownloadData = new DataUnits();
            CurrentSessionUploadData = new DataUnits();
            SpeedGraph = new NetworkSpeedGraph();
            Date = "";
            TotalUsageText = "";
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
