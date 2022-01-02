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
        public DataUnits DownloadSpeed { get; set; }
        public DataUnits UploadSpeed { get; set; }
        public DataUnits TotalDownloadData { get; set; }
        public DataUnits TotalUploadData { get; set; }
        public DataUnits CurrentSessionDownloadData { get; set; }
        public DataUnits CurrentSessionUploadData { get; set; }

        private PointCollection ltPoint;
        public PointCollection LtPoint
        {
            get { return ltPoint; }
            set
            {
                ltPoint = value; OnPropertyChanged("LtPoint");
            }
        }

        public DataUsageSummaryVM()
        {
            DownloadSpeed = new DataUnits();
            UploadSpeed = new DataUnits();
            TotalDownloadData = new DataUnits();
            TotalUploadData = new DataUnits();
            CurrentSessionDownloadData = new DataUnits();
            CurrentSessionUploadData = new DataUnits();

            LtPoint = new PointCollection
            {
                new Point(0, 120),
                new Point(50, 20),
                new Point(75, 20),
                new Point(550, 20)
            };
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
