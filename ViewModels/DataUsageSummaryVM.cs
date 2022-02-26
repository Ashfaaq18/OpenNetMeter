using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels
{
    public class MyLine : INotifyPropertyChanged
    {
        private Point from;
        public Point From 
        {
            get { return from; }
            set
            {
                from = value;
                OnPropertyChanged("From");
            }
        }

        private Point to; 
        public Point To 
        { 
            get { return to; }
            set
            {
                to = value;
                OnPropertyChanged("To");
            }
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

    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
        public TrayPopupVM tpvm;

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

        public double StartGraphWidth { get; set; }
        public double StartGraphHeight{ get; set; }
        public double GraphWidth { get; set; }
        public double GraphHeight { get; set; }

        private double xstart;
        public double Xstart
        {
            get { return xstart; }
            set
            {
                xstart = value;
                OnPropertyChanged("Xstart");
            }
        }

        public ObservableCollection<MyLine> Lines { get; private set; }
        public List<MyLine> Points { get; private set; }
        public DataUsageSummaryVM(ref TrayPopupVM tpvm_ref)
        {
            tpvm = tpvm_ref;
            TotalDownloadData = 0;
            TotalUploadData = 0;
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            //SpeedGraph = new NetworkSpeedGraph();
            TotalUsageText = "Total data usage of the past 0 days";
            Lines = new ObservableCollection<MyLine>();
            Points = new List<MyLine>();
            for(int i = 0; i<60; i++)
            {
                Lines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                Points.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
            }
            //DrawPoints();

        }
        private void DrawPoints()
        {
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Operation Started : draw");
                    int i = 0;
                    while (true)
                    {
                        await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                        {
                            Points[i].From = new Point( (GraphWidth / 60.0) * i , 50);
                            Points[i].To = new Point( (GraphWidth / 60.0) * (i+1), 50);
                            Lines[i].From = new Point( Xstart + Points[i].From.X * (GraphWidth / StartGraphWidth), Points[i].From.Y * (GraphHeight / StartGraphHeight) );
                            Lines[i].To = new Point( Xstart + Points[i].To.X * (GraphWidth / StartGraphWidth), Points[i].To.Y * (GraphHeight / StartGraphHeight));
                            //DownloadPolyPath.Points.Add(new Point((LineX2Label.ActualWidth + 5.0) + Graph.ActualWidth / 60 * i, Graph.ActualHeight / 2));
                            //this.DownloadPolyPathBind = tempPC;
                        }));
                        //await Task.Delay(1000);
                        i++;
                        if (i >= 60)
                        {
                            i = 0;
                            await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                for (int i = 0; i < 60; i++)
                                {
                                    Points[i].From = new Point(0, 0);
                                    Points[i].To = new Point(0, 0);
                                    Lines[i].From = new Point(0, 0);
                                    Lines[i].To = new Point(0, 0);
                                }
                                //this.DownloadPolyPathBind = tempPC;
                                //DownloadPolyPath.Points.Clear();
                            }));
                        }
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
                }
            });
        }

        private double ConvToGraphCoords(ulong value, double height)
        {
            if ((double)value > Math.Pow(1024, 2))
            {
                return (height) * ((1.0 / 3.0) - ((double)value) / (1024.0 * 1024.0 * 1024.0 * 3.0));
            }
            else if ((double)value > Math.Pow(1024, 1))
            {
                return (height) * ((2.0 / 3.0) - ((double)value) / (1024.0 * 1024.0 * 3.0));
            }
            else
            {
                return (height) * ((3.0 / 3.0) - ((double)value) / (1024.0 * 3.0));
            }
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
