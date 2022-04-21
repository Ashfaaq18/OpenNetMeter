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
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

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
    public class Graph
    {
        public List<TextBlock> Xlabels { get; private set; }
        public List<TextBlock> Ylabels { get; private set; }
        public List<MyLine> XLines { get; set; }
        public List<MyLine> YLines { get; set; }
        public List<MyLine> Borders { get; set; }
        public ObservableCollection<MyLine> DownloadLines { get; private set; }
        public ObservableCollection<MyLine> UploadLines { get; private set; }
        public List<MyLine> DownloadPoints { get; private set; }
        public List<MyLine> UploadPoints { get; private set; }

        public double X = 50;
        public double Y = 50;
        public Graph()
        {
            DownloadPoints = new List<MyLine>();
            UploadPoints = new List<MyLine>();
            DownloadLines = new ObservableCollection<MyLine>();
            UploadLines = new ObservableCollection<MyLine>();
            Xlabels = new List<TextBlock>();
            Ylabels = new List<TextBlock>();
            XLines = new List<MyLine>();
            YLines = new List<MyLine>();
            Borders = new List<MyLine>();
        }
    }


    public class DataUsageSummaryVM : INotifyPropertyChanged
    {
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

        public ulong UploadSpeed { get; set; }
        public ulong DownloadSpeed { get; set; }

        public double GraphWidth { get; set; }
        public double GraphHeight { get; set; }
        public double Xstart { get; set; }
        public bool pauseDraw { get; set; }
        
        public Graph MyGraph { get; set; }

        private Size maxYtextSize;
        const int GridXCount = 7;
        const int GridYCount = 7;
        public int XaxisResolution { get; set; }
        public DataUsageSummaryVM()
        {
            TotalDownloadData = 0;
            TotalUploadData = 0;
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            UploadSpeed = 0;
            DownloadSpeed = 0;
            TotalUsageText = "Total data usage of the past 0 days";

            MyGraph = new Graph();

            XaxisResolution = ( GridYCount - 1 ) * 10;
            maxYtextSize = ShapeMeasure(new TextBlock { Text = "0512Mb", FontSize = 11, Padding = new Thickness(0) });
            maxYtextSize.Width += 2.0;
            Xstart = maxYtextSize.Width;

            for (int i = 0; i< XaxisResolution; i++)
            {
                MyGraph.DownloadLines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                MyGraph.UploadLines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                MyGraph.DownloadPoints.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                MyGraph.UploadPoints.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) }); 
            }

            //Xlabels
            for (int i = 0; i < GridYCount; i++)
            {
                if (i < GridYCount - 1)
                {
                    MyGraph.Xlabels.Add(
                    new TextBlock
                    {
                        Text = (i*10).ToString(),
                        FontSize = 11,
                        Padding = new Thickness(0)
                    });
                }
                else
                {
                    MyGraph.Xlabels.Add(
                    new TextBlock
                    {
                        Text = "seconds",
                        FontSize = 11,
                        Padding = new Thickness(0)
                    });
                }

                if (i > 0 && i < GridYCount - 1)
                {
                    MyGraph.YLines.Add(new MyLine());
                }
            }
            //Ylabels
            ulong temp = 1;
            for (int i = 0; i < GridXCount; i++)
            {

                if (i == 0 || i == GridXCount-1)
                {
                    MyGraph.Ylabels.Add(new TextBlock
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

                    MyGraph.Ylabels.Add(new TextBlock
                    {
                        Text = DataSizeSuffix.SizeSuffixInStr(temp, 1, false),
                        FontSize = 11,
                        Padding = new Thickness(0, 0, 0, 0)
                    });

                    MyGraph.XLines.Add(new MyLine());
                }  
            }

            for(int i = 0; i<4; i++)
            {
                MyGraph.Borders.Add(new MyLine());
            }

            DrawPoints();
        }
        public Size ShapeMeasure(TextBlock tb)
        {
            // Measured Size is bounded to be less than maxSize
            Size maxSize = new Size(
                 double.PositiveInfinity,
                 double.PositiveInfinity);
            tb.Measure(maxSize);
            return tb.DesiredSize;
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
                        if (drawPointCount >= XaxisResolution)
                        {
                            await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                //shift xaxis label
                                for (int i = 0; i< MyGraph.Xlabels.Count/2; i++)
                                {
                                    string temp = MyGraph.Xlabels[i].Text;
                                    MyGraph.Xlabels[i].Text = MyGraph.Xlabels[i + MyGraph.Xlabels.Count / 2].Text;
                                    MyGraph.Xlabels[i + MyGraph.Xlabels.Count / 2].Text = temp;
                                }
                            }));

                            drawPointCount = XaxisResolution/2;
                            for (int i = 0; i < MyGraph.DownloadPoints.Count; i++)
                            {
                                if(i < XaxisResolution/2)
                                {
                                    MyGraph.DownloadPoints[i].From = new Point(MyGraph.DownloadPoints[XaxisResolution / 2 + i].From.X - XaxisResolution / 2, MyGraph.DownloadPoints[XaxisResolution / 2 + i].From.Y);
                                    MyGraph.DownloadPoints[i].To = new Point(MyGraph.DownloadPoints[XaxisResolution / 2 + i].To.X - XaxisResolution / 2, MyGraph.DownloadPoints[XaxisResolution / 2 + i].To.Y);
                                }
                                else
                                {
                                    MyGraph.DownloadPoints[i].From = new Point(0, 0);
                                    MyGraph.DownloadPoints[i].To = new Point(0, 0);
                                }
                                
                            }

                            for (int i = 0; i < MyGraph.UploadPoints.Count; i++)
                            {
                                if(i < XaxisResolution/2)
                                {
                                    MyGraph.UploadPoints[i].From = new Point(MyGraph.UploadPoints[XaxisResolution / 2 + i].From.X - XaxisResolution / 2, MyGraph.UploadPoints[XaxisResolution / 2 + i].From.Y);
                                    MyGraph.UploadPoints[i].To = new Point(MyGraph.UploadPoints[XaxisResolution / 2 + i].To.X - XaxisResolution / 2, MyGraph.UploadPoints[XaxisResolution / 2 + i].To.Y);
                                }
                                else
                                {
                                    MyGraph.UploadPoints[i].From = new Point(0, 0);
                                    MyGraph.UploadPoints[i].To = new Point(0, 0);
                                }
                                
                            }

                            if (!pauseDraw)
                            { 
                                //reset the chart
                                await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
                                {
                                    for (int i = 0; i < MyGraph.DownloadLines.Count; i++)
                                    {
                                        MyGraph.DownloadLines[i].From = new Point(Xstart + MyGraph.DownloadPoints[i].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.DownloadPoints[i].From.Y, GraphHeight));
                                        MyGraph.DownloadLines[i].To = new Point(Xstart + MyGraph.DownloadPoints[i].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.DownloadPoints[i].To.Y, GraphHeight));
                                    }
                                    for (int i = 0; i < MyGraph.UploadLines.Count; i++)
                                    {
                                        MyGraph.UploadLines[i].From = new Point(Xstart + MyGraph.UploadPoints[i].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.UploadPoints[i].From.Y, GraphHeight));
                                        MyGraph.UploadLines[i].To = new Point(Xstart + MyGraph.UploadPoints[i].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.UploadPoints[i].To.Y, GraphHeight));
                                    }
                                }));
                            }
                        }

                        if(drawPointCount == 0)
                        {
                            MyGraph.DownloadPoints[drawPointCount].From = new Point(drawPointCount, 0);
                            MyGraph.DownloadPoints[drawPointCount].To = new Point((drawPointCount + 1), DownloadSpeed);

                            MyGraph.UploadPoints[drawPointCount].From = new Point(drawPointCount, 0);
                            MyGraph.UploadPoints[drawPointCount].To = new Point((drawPointCount + 1), UploadSpeed);
                        }
                        else
                        {
                            MyGraph.DownloadPoints[drawPointCount].From = new Point(drawPointCount, MyGraph.DownloadPoints[drawPointCount-1].To.Y);
                            MyGraph.DownloadPoints[drawPointCount].To = new Point((drawPointCount + 1), DownloadSpeed);

                            MyGraph.UploadPoints[drawPointCount].From = new Point(drawPointCount, MyGraph.UploadPoints[drawPointCount-1].To.Y);
                            MyGraph.UploadPoints[drawPointCount].To = new Point((drawPointCount + 1), UploadSpeed);
                        }
                        
                        if (!pauseDraw)
                        {
                            await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                MyGraph.DownloadLines[drawPointCount].From = new Point(Xstart + MyGraph.DownloadPoints[drawPointCount].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.DownloadPoints[drawPointCount].From.Y, GraphHeight));
                                MyGraph.DownloadLines[drawPointCount].To = new Point(Xstart + MyGraph.DownloadPoints[drawPointCount].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.DownloadPoints[drawPointCount].To.Y, GraphHeight));

                                MyGraph.UploadLines[drawPointCount].From = new Point(Xstart + MyGraph.UploadPoints[drawPointCount].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.UploadPoints[drawPointCount].From.Y, GraphHeight));
                                MyGraph.UploadLines[drawPointCount].To = new Point(Xstart + MyGraph.UploadPoints[drawPointCount].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(MyGraph.UploadPoints[drawPointCount].To.Y, GraphHeight));
                            
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
