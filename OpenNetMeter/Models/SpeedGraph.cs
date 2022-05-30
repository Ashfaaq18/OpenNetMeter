using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OpenNetMeter.Models
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
    public class SpeedGraph
    {
        // ---------- Graph geometries ------------//
        public List<MyLine> XLines { get; set; }
        public List<MyLine> YLines { get; set; }
        public List<MyLine> Borders { get; set; }
        public ObservableCollection<MyLine> DownloadLines { get; private set; }
        public ObservableCollection<MyLine> UploadLines { get; private set; }
        public List<MyLine> DownloadPoints { get; private set; }
        public List<MyLine> UploadPoints { get; private set; }

        // ----------- Graph Labels ----------------//
        public List<TextBlock> Xlabels { get; private set; }
        public double Xstart { get; set; }
        public List<TextBlock> Ylabels { get; private set; }
        private Size maxYtextSize;

        // ----------- Other Graph info ----------- //
        public double GraphWidth { get; set; }
        public double GraphHeight { get; set; }

        public int GridXCount;
        public int GridYCount;

        public int XaxisResolution { get; set; } 
        public bool resumeDraw { get; set; }
        public ulong UploadSpeed { get; set; }
        public ulong DownloadSpeed { get; set; }
        public bool firstDrawAfterResume { get; set; }
        public SpeedGraph(int XlineCount, int YlineCount)
        {
            GridXCount = XlineCount;
            GridYCount = YlineCount;
            DownloadPoints = new List<MyLine>();
            UploadPoints = new List<MyLine>();
            DownloadLines = new ObservableCollection<MyLine>();
            UploadLines = new ObservableCollection<MyLine>();
            Xlabels = new List<TextBlock>();
            Ylabels = new List<TextBlock>();
            XLines = new List<MyLine>();
            YLines = new List<MyLine>();
            Borders = new List<MyLine>();

            firstDrawAfterResume = false;
        }

        public void InitGraph()
        {
            XaxisResolution = (GridYCount - 1) * 10;
            maxYtextSize =  UIMeasure.Shape(new TextBlock { Text = "0512Mb", FontSize = 11, Padding = new Thickness(0) });
            maxYtextSize.Width += 2.0;
            Xstart = maxYtextSize.Width;

            // populate the lists
            for (int i = 0; i < XaxisResolution; i++)
            {
                DownloadLines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                UploadLines.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                DownloadPoints.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
                UploadPoints.Add(new MyLine { From = new Point(0, 0), To = new Point(0, 0) });
            }

            // Add X labels and Y lines
            for (int i = 0; i < GridYCount; i++)
            {
                Xlabels.Add(
                new TextBlock
                {
                    Text = i < (GridYCount - 1) ? (i * 10).ToString() : "seconds",
                    FontSize = 11,
                    Padding = new Thickness(0)
                });

                if (i > 0 && i < GridYCount - 1)
                {
                    YLines.Add(new MyLine());
                }
            }

            // Add Y labels and X lines
            ulong temp = 1;
            for (int i = 0; i < GridXCount; i++)
            {

                if (i == 0 || i == GridXCount - 1)
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

                    XLines.Add(new MyLine());
                }
            }

            // Add borders
            for (int i = 0; i < 4; i++)
            {
                Borders.Add(new MyLine());
            }
        }

        private int drawPointCount;
        public void DrawPoints()
        {
            Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        // reset the graph after it completes a full run
                        if (drawPointCount >= XaxisResolution)
                        {
                            await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                            {
                                //shift xaxis label
                                for (int i = 0; i < Xlabels.Count / 2; i++)
                                {
                                    string temp = Xlabels[i].Text;
                                    Xlabels[i].Text = Xlabels[i + Xlabels.Count / 2].Text;
                                    Xlabels[i + Xlabels.Count / 2].Text = temp;
                                }
                            }));

                            drawPointCount = XaxisResolution / 2;
                            for (int i = 0; i < DownloadPoints.Count; i++)
                            {
                                if (i < XaxisResolution / 2)
                                {
                                    DownloadPoints[i].From = new Point(DownloadPoints[XaxisResolution / 2 + i].From.X - XaxisResolution / 2, DownloadPoints[XaxisResolution / 2 + i].From.Y);
                                    DownloadPoints[i].To = new Point(DownloadPoints[XaxisResolution / 2 + i].To.X - XaxisResolution / 2, DownloadPoints[XaxisResolution / 2 + i].To.Y);
                                }
                                else
                                {
                                    DownloadPoints[i].From = new Point(0, 0);
                                    DownloadPoints[i].To = new Point(0, 0);
                                }

                            }

                            for (int i = 0; i < UploadPoints.Count; i++)
                            {
                                if (i < XaxisResolution / 2)
                                {
                                    UploadPoints[i].From = new Point(UploadPoints[XaxisResolution / 2 + i].From.X - XaxisResolution / 2, UploadPoints[XaxisResolution / 2 + i].From.Y);
                                    UploadPoints[i].To = new Point(UploadPoints[XaxisResolution / 2 + i].To.X - XaxisResolution / 2, UploadPoints[XaxisResolution / 2 + i].To.Y);
                                }
                                else
                                {
                                    UploadPoints[i].From = new Point(0, 0);
                                    UploadPoints[i].To = new Point(0, 0);
                                }

                            }

                            if (resumeDraw)
                            {
                                //reset the chart
                                await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    for (int i = 0; i < DownloadLines.Count; i++)
                                    {
                                        DownloadLines[i].From = new Point(Xstart + DownloadPoints[i].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(DownloadPoints[i].From.Y, GraphHeight));
                                        DownloadLines[i].To = new Point(Xstart + DownloadPoints[i].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(DownloadPoints[i].To.Y, GraphHeight));
                                    }
                                    for (int i = 0; i < UploadLines.Count; i++)
                                    {
                                        UploadLines[i].From = new Point(Xstart + UploadPoints[i].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(UploadPoints[i].From.Y, GraphHeight));
                                        UploadLines[i].To = new Point(Xstart + UploadPoints[i].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(UploadPoints[i].To.Y, GraphHeight));
                                    }
                                }));
                            }
                        }

                        if (drawPointCount == 0)
                        {
                            DownloadPoints[drawPointCount].From = new Point(0, 0);
                            DownloadPoints[drawPointCount].To = new Point(1, DownloadSpeed);

                            UploadPoints[drawPointCount].From = new Point(0, 0);
                            UploadPoints[drawPointCount].To = new Point(1, UploadSpeed);
                        }
                        else
                        {
                            DownloadPoints[drawPointCount].From = new Point(drawPointCount, DownloadPoints[drawPointCount - 1].To.Y);
                            DownloadPoints[drawPointCount].To = new Point((drawPointCount + 1), DownloadSpeed);

                            UploadPoints[drawPointCount].From = new Point(drawPointCount, UploadPoints[drawPointCount - 1].To.Y);
                            UploadPoints[drawPointCount].To = new Point((drawPointCount + 1), UploadSpeed);
                        }

                        if (resumeDraw)
                        {
                            await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                            {
                                if(firstDrawAfterResume && drawPointCount > 0)
                                {
                                    DownloadLines[drawPointCount-1].From = new Point(Xstart + DownloadPoints[drawPointCount-1].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(DownloadPoints[drawPointCount-1].From.Y, GraphHeight));
                                    DownloadLines[drawPointCount-1].To = new Point(Xstart + DownloadPoints[drawPointCount-1].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(DownloadPoints[drawPointCount-1].To.Y, GraphHeight));

                                    UploadLines[drawPointCount-1].From = new Point(Xstart + UploadPoints[drawPointCount-1].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(UploadPoints[drawPointCount-1].From.Y, GraphHeight));
                                    UploadLines[drawPointCount-1].To = new Point(Xstart + UploadPoints[drawPointCount-1].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(UploadPoints[drawPointCount-1].To.Y, GraphHeight));
                                }

                                firstDrawAfterResume = false;

                                DownloadLines[drawPointCount].From = new Point(Xstart + DownloadPoints[drawPointCount].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(DownloadPoints[drawPointCount].From.Y, GraphHeight));
                                DownloadLines[drawPointCount].To = new Point(Xstart + DownloadPoints[drawPointCount].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(DownloadPoints[drawPointCount].To.Y, GraphHeight));

                                UploadLines[drawPointCount].From = new Point(Xstart + UploadPoints[drawPointCount].From.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(UploadPoints[drawPointCount].From.Y, GraphHeight));
                                UploadLines[drawPointCount].To = new Point(Xstart + UploadPoints[drawPointCount].To.X * (GraphWidth / (double)XaxisResolution), ConvToGraphCoords(UploadPoints[drawPointCount].To.Y, GraphHeight));

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
    }
}
