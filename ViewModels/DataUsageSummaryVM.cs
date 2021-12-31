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
        public DataUnits TotalDownloadData { get; set; }

        public DataUnits TotalUploadData { get; set; }

        public DataUnits CurrentSessionDownloadData { get; set; }

        private PointCollection ltPoint;
        public PointCollection LtPoint
        {
            get { return ltPoint; }
            set
            {
                ltPoint = value; OnPropertyChanged("LtPoint");
            }
        }
        public DataUnits CurrentSessionUploadData { get; set; }

        public DataUsageSummaryVM()
        {
            TotalDownloadData = new DataUnits();
            TotalUploadData = new DataUnits();
            CurrentSessionDownloadData = new DataUnits();
            CurrentSessionUploadData = new DataUnits();

            LtPoint = new PointCollection();
            LtPoint.Add(new Point(0, 120));
            LtPoint.Add(new Point(50, 20));
            LtPoint.Add(new Point(75, 20));
            LtPoint.Add(new Point(550, 20));
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
