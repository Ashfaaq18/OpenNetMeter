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

        public ObservableCollection<TextBlock> Xlabels { get; private set; }
        public ObservableCollection<TextBlock> Ylabels { get; private set; }
        public ObservableCollection<MyLine> DownloadLines { get; private set; }
        public ObservableCollection<MyLine> UploadLines { get; private set; }
        public List<MyLine> DownloadPoints { get; private set; }
        public List<MyLine> UploadPoints { get; private set; }

        const int GridXCount = 7;
        const int GridYCount = 7;
        public int XaxisRange { get; set; }
        public DataUsageSummaryVM(ref TrayPopupVM tpvm_ref)
        {
            tpvm = tpvm_ref;
            TotalDownloadData = 0;
            TotalUploadData = 0;
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            TotalUsageText = "Total data usage of the past 0 days";
            DownloadPoints = new List<MyLine>();
            UploadPoints = new List<MyLine>();
            DownloadLines = new ObservableCollection<MyLine>();
            UploadLines = new ObservableCollection<MyLine>();
            Xlabels = new ObservableCollection<TextBlock>();
            Ylabels = new ObservableCollection<TextBlock>();
            XaxisRange = 60;
            for (int i = 0; i< XaxisRange; i++)
            {
                DownloadLines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                UploadLines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                DownloadPoints.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                UploadPoints.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) }); 
            }

            //Xlabels
            for (int i = 0; i < GridYCount; i++)
            {
                if (i < GridYCount - 1)
                {
                    Xlabels.Add(
                    new TextBlock
                    {
                        Text = (i*10).ToString(),
                        FontSize = 11,
                        Padding = new Thickness(0)
                    });
                }
                else
                {
                    Xlabels.Add(
                    new TextBlock
                    {
                        Text = "seconds",
                        FontSize = 11,
                        Padding = new Thickness(0)
                    });
                }
            }
            //Ylabels
            ulong temp = 1;
            for (int i = 0; i < GridXCount; i++)
            {
                if (i == 0 || i == GridXCount-1)
                {
                    Ylabels.Add(new TextBlock
                    {
                        Text = "",
                        FontSize = 11,
                        Padding = new Thickness(0, 0, 0, 0)
                    });
                }
                else
                {
                    if (i % 2 == 0)
                        temp *= 2;
                    else
                        temp *= 512;

                    Ylabels.Add(new TextBlock
                    {
                        Text = DataSizeSuffix.SizeSuffixInStr(temp, 1, false),
                        FontSize = 11,
                        Padding = new Thickness(0, 0, 0, 0)
                    });
                }  
            }

            DrawPoints();
        }
        
        private int drawPointCount = 0;
        public void DrawPoints()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        if (drawPointCount >= XaxisRange)
                        {
                            await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                //shift xaxis label
                                for (int i = 0; i<Xlabels.Count/2; i++)
                                {
                                    string temp = Xlabels[i].Text;
                                    Xlabels[i].Text = Xlabels[i + Xlabels.Count / 2].Text;
                                    Xlabels[i + Xlabels.Count / 2].Text = temp;
                                }
                            }));

                            drawPointCount = XaxisRange/2;
                            for (int i = 0; i < DownloadPoints.Count; i++)
                            {
                                if(i < XaxisRange/2)
                                {
                                    DownloadPoints[i].From = new Point(DownloadPoints[XaxisRange / 2 + i].From.X - XaxisRange / 2, DownloadPoints[XaxisRange / 2 + i].From.Y);
                                    DownloadPoints[i].To = new Point(DownloadPoints[XaxisRange / 2 + i].To.X - XaxisRange / 2, DownloadPoints[XaxisRange / 2 + i].To.Y);
                                }
                                else
                                {
                                    DownloadPoints[i].From = new Point(0, 0);
                                    DownloadPoints[i].To = new Point(0, 0);
                                }
                                
                            }

                            for (int i = 0; i < UploadPoints.Count; i++)
                            {
                                if(i < XaxisRange/2)
                                {
                                    UploadPoints[i].From = new Point(UploadPoints[XaxisRange / 2 + i].From.X - XaxisRange / 2, UploadPoints[XaxisRange / 2 + i].From.Y);
                                    UploadPoints[i].To = new Point(UploadPoints[XaxisRange / 2 + i].To.X - XaxisRange / 2, UploadPoints[XaxisRange / 2 + i].To.Y);
                                }
                                else
                                {
                                    UploadPoints[i].From = new Point(0, 0);
                                    UploadPoints[i].To = new Point(0, 0);
                                }
                                
                            }

                            if (!pauseDraw)
                            { 
                                //reset the chart
                                await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
                                {
                                    for (int i = 0; i < DownloadLines.Count; i++)
                                    {
                                        DownloadLines[i].From = new Point(Xstart + DownloadPoints[i].From.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(DownloadPoints[i].From.Y, GraphHeight));
                                        DownloadLines[i].To = new Point(Xstart + DownloadPoints[i].To.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(DownloadPoints[i].To.Y, GraphHeight));
                                    }
                                    for (int i = 0; i < UploadLines.Count; i++)
                                    {
                                        UploadLines[i].From = new Point(Xstart + UploadPoints[i].From.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(UploadPoints[i].From.Y, GraphHeight));
                                        UploadLines[i].To = new Point(Xstart + UploadPoints[i].To.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(UploadPoints[i].To.Y, GraphHeight));
                                    }
                                }));
                            }
                        }

                        if(drawPointCount == 0)
                        {
                            DownloadPoints[drawPointCount].From = new Point(drawPointCount, 0);
                            DownloadPoints[drawPointCount].To = new Point((drawPointCount + 1), tpvm.DownloadSpeed);
                        
                            UploadPoints[drawPointCount].From = new Point(drawPointCount, 0);
                            UploadPoints[drawPointCount].To = new Point((drawPointCount + 1), tpvm.UploadSpeed);
                        }
                        else
                        {
                            DownloadPoints[drawPointCount].From = new Point(drawPointCount, DownloadPoints[drawPointCount-1].To.Y);
                            DownloadPoints[drawPointCount].To = new Point((drawPointCount + 1), tpvm.DownloadSpeed);

                            UploadPoints[drawPointCount].From = new Point(drawPointCount, UploadPoints[drawPointCount-1].To.Y);
                            UploadPoints[drawPointCount].To = new Point((drawPointCount + 1), tpvm.UploadSpeed);
                        }
                        
                        if (!pauseDraw)
                        {
                            await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                DownloadLines[drawPointCount].From = new Point(Xstart + DownloadPoints[drawPointCount].From.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(DownloadPoints[drawPointCount].From.Y, GraphHeight));
                                DownloadLines[drawPointCount].To = new Point(Xstart + DownloadPoints[drawPointCount].To.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(DownloadPoints[drawPointCount].To.Y, GraphHeight));
                                
                                UploadLines[drawPointCount].From = new Point(Xstart + UploadPoints[drawPointCount].From.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(UploadPoints[drawPointCount].From.Y, GraphHeight));
                                UploadLines[drawPointCount].To = new Point(Xstart + UploadPoints[drawPointCount].To.X * (GraphWidth / (double)XaxisRange), ConvToGraphCoords(UploadPoints[drawPointCount].To.Y, GraphHeight));
                            
                            }));
                        }
                        drawPointCount++;
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
