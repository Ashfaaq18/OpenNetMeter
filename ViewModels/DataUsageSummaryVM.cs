using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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

        public double GraphWidth { get; set; }
        public double GraphHeight { get; set; }
        public double Xstart { get; set; }
        public bool pauseDraw { get; set; }

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
            Points = new List<MyLine>();
            Lines = new ObservableCollection<MyLine>();
            for (int i = 0; i<60; i++)
            {
                Lines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                Points.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
            }

            DrawPoints();
        }

        private int drawPointCount = 0;
        public void DrawPoints()
        {
           //cts = new CancellationTokenSource();
           // ct = cts.Token;
            Task.Run(async () =>
            {
                try
                {
                    //Debug.WriteLine("Operation Started : draw");
                    //while (!ct.IsCancellationRequested)
                    while (true)
                    {
                        if (drawPointCount >= 60)
                        {
                            drawPointCount = 0;
                            for (int i = 0; i < Points.Count; i++)
                            {
                                Points[i].From = new Point(0, 0);
                                Points[i].To = new Point(0, 0);
                            }

                            if (!pauseDraw)
                            {
                                await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                                {
                                    for (int i = 0; i < Lines.Count; i++)
                                    {
                                        Lines[i].From = new Point(0, 0);
                                        Lines[i].To = new Point(0, 0);
                                    }
                                    //this.DownloadPolyPathBind = tempPC;
                                    //DownloadPolyPath.Points.Clear();
                                }));
                            }
                        }
                        if(drawPointCount == 0)
                        {
                            Points[drawPointCount].From = new Point(drawPointCount, 0);
                            Points[drawPointCount].To = new Point((drawPointCount + 1), tpvm.DownloadSpeed);
                        }
                        else
                        {
                            Points[drawPointCount].From = new Point(drawPointCount, Points[drawPointCount-1].To.Y);
                            Points[drawPointCount].To = new Point((drawPointCount + 1), tpvm.DownloadSpeed);
                        }
                        
                        if (!pauseDraw)
                        {
                            await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                Lines[drawPointCount].From = new Point(Xstart + Points[drawPointCount].From.X * (GraphWidth / 60.0), ConvToGraphCoords(Points[drawPointCount].From.Y, GraphHeight));
                                Lines[drawPointCount].To = new Point(Xstart + Points[drawPointCount].To.X * (GraphWidth / 60.0), ConvToGraphCoords(Points[drawPointCount].To.Y, GraphHeight));
                            }));
                        }
                        drawPointCount++;
                        //await Task.Delay(1000, ct);
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
                }
            });
        }


        public double ConvToGraphCoords(double value, double height)
        {
            if (value > Math.Pow(1024, 2))
            {
                return (height) * ((1.0 / 3.0) - (value) / (1024.0 * 1024.0 * 1024.0 * 3.0));
            }
            else if (value > Math.Pow(1024, 1))
            {
                return (height) * ((2.0 / 3.0) - (value) / (1024.0 * 1024.0 * 3.0));
            }
            else
            {
                return (height) * ((3.0 / 3.0) - (value) / (1024.0 * 3.0));
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
