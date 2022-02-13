using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenNetMeter.Models
{
    public class NetworkSpeedGraph : INotifyPropertyChanged
    {
        private WriteableBitmap graph;
        public WriteableBitmap Graph
        {
            get { return graph; }
            set
            {
                graph = value; OnPropertyChanged("Graph");
            }
        }

        public List<string> Yaxis { get; set; }
        public ObservableCollection<int> Xaxis { get; set; }
        public ulong DownloadSpeed { get; set; }
        public ulong UploadSpeed { get; set; }
        public NetworkSpeedGraph()
        {
            Xaxis = new ObservableCollection<int>();
            Yaxis = new List<string>();

            const int resolution = 122; //
            const int width = 540;
            const int height = 120;

            for (int i = 0; i < 6; i++) // 60 secs axis
                Xaxis.Add(i * 10);

            ulong temp = 512;
            for (int i = 0; i < 6; i++)
            {
                if (i != 0)
                {
                    if (i % 2 == 0)
                        temp *= 512;
                    else
                        temp *= 2;
                }
                Yaxis.Add(DataSizeSuffix.SizeSuffixInStr(temp,1,false));
            }

            Yaxis.Reverse();
            
            Graph = BitmapFactory.New(width, height);
            using (Graph.GetBitmapContext())
            {
                // Clear the WriteableBitmap with white color
                Graph.Clear(Colors.White);
                
                for (int i = 1; i < Xaxis.Count; i++)
                    Graph.DrawLine(i * width / Xaxis.Count, 0, i * width / Xaxis.Count, height, Colors.LightGray);

                for (int i = 1; i < Yaxis.Count; i++)
                    Graph.DrawLine(0, i * height / Yaxis.Count, width, i * height / Yaxis.Count, Colors.LightGray);

                //border
                Graph.DrawRectangle(0, 0, width, height, Colors.Black);

                int[] points_Download = new int[resolution];
                int[] points_Upload = new int[resolution];
                for (int i = 0; i < resolution; i++)
                {
                    points_Download[i] = 0;
                    points_Upload[i] = 0;
                }
                    
                Task.Run(async () =>
                {
                    //first init
                    for (int i = 0; i < resolution; i += 2)
                    {
                        if (i == 0)
                        {
                            points_Download[i] = 0;
                            points_Download[i + 1] = height;
                            points_Upload[i] = 0;
                            points_Upload[i + 1] = height;
                            await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                Graph.DrawLineAa(points_Upload[i], points_Upload[i + 1], points_Upload[i], points_Upload[i + 1], Colors.LightSalmon, 2);
                                Graph.FillEllipseCentered(points_Upload[i], points_Upload[i + 1], 2, 2, Colors.LightSalmon);
                                Graph.DrawLineAa(points_Download[i], points_Download[i + 1], points_Download[i], points_Download[i + 1], Colors.LightSeaGreen, 2);
                                Graph.FillEllipseCentered(points_Download[i], points_Download[i + 1], 2, 2, Colors.LightSeaGreen);
                            }));
                        }
                        else
                        {
                            points_Download[i] = (width / ((resolution / 2) - 1)) * i / 2;
                            points_Upload[i] = (width / ((resolution / 2) - 1)) * i / 2;
                            //points_Download[i + 1] = new Random().Next(0, height);
                            points_Download[i + 1] = ConvToGraphCoords(DownloadSpeed, height);
                            points_Upload[i + 1] = ConvToGraphCoords(UploadSpeed, height);
                            //Debug.WriteLine(DownloadSpeed + ": " + points_Download[i + 1] + "," + UploadSpeed + ": " + points_Upload[i + 1]);
                            await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                Graph.DrawLineAa(points_Upload[i - 2], points_Upload[i - 1], points_Upload[i], points_Upload[i + 1], Colors.LightSalmon, 2);
                                Graph.FillEllipseCentered(points_Upload[i], points_Upload[i + 1], 2, 2, Colors.LightSalmon);
                                Graph.DrawLineAa(points_Download[i - 2], points_Download[i - 1], points_Download[i], points_Download[i + 1], Colors.LightSeaGreen, 2);
                                Graph.FillEllipseCentered(points_Download[i], points_Download[i + 1], 2, 2, Colors.LightSeaGreen);                    
                            }));                
                        }
                        await Task.Delay(1000);
                    }

                    int j = 0;
                    while (true)
                    {
                        //move axis
                        for (int i = 0; i < Xaxis.Count; i++)
                        {
                            if (j == 0)
                            {
                                if (i < Xaxis.Count / 2)
                                    await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Xaxis[i] += 30));
                                else
                                    await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Xaxis[i] -= 30));
                            }
                            else
                            {
                                if (i < Xaxis.Count / 2)
                                    await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Xaxis[i] -= 30));
                                else
                                    await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Xaxis[i] += 30));
                            }
                        }


                        //clear old graph
                        await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Graph.Clear(Colors.White)));

                        //redraw borders
                        for (int i = 1; i < Xaxis.Count; i++)
                            await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Graph.DrawLine(i * width / Xaxis.Count, 0, i * width / Xaxis.Count, height, Colors.LightGray)));

                        for (int i = 1; i < Yaxis.Count; i++)
                            await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Graph.DrawLine(0, i * height / Yaxis.Count, width, i * height / Yaxis.Count, Colors.LightGray)));

                        await Application.Current.Dispatcher?.BeginInvoke((Action)(() => Graph.DrawRectangle(0, 0, width, height, Colors.Black)));

                        //shift graph halfway back and start plotting new to show continuation
                        for (int i = 0; i < resolution; i += 2)
                        {
                            if (i <= resolution / 2 )
                            {
                                points_Download[i + 1] = points_Download[i + resolution / 2 ];
                                points_Download[i + resolution / 2 ] = 0;
                                points_Upload[i + 1] = points_Upload[i + resolution / 2 ];
                                points_Upload[i + resolution / 2 ] = 0;
                                if (i == 0)
                                {
                                    await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                                    {
                                        Graph.DrawLineAa(points_Upload[i], points_Upload[i + 1], points_Upload[i], points_Upload[i + 1], Colors.LightSalmon, 2);
                                        Graph.FillEllipseCentered(points_Upload[i], points_Upload[i + 1], 2, 2, Colors.LightSalmon);
                                        Graph.DrawLineAa(points_Download[i], points_Download[i + 1], points_Download[i], points_Download[i + 1], Colors.LightSeaGreen, 2);
                                        Graph.FillEllipseCentered(points_Download[i], points_Download[i + 1], 2, 2, Colors.LightSeaGreen);
                                    }));
                                }
                                else
                                    await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                                    {
                                        Graph.DrawLineAa(points_Upload[i - 2], points_Upload[i - 1], points_Upload[i], points_Upload[i + 1], Colors.LightSalmon, 2);
                                        Graph.FillEllipseCentered(points_Upload[i], points_Upload[i + 1], 2, 2, Colors.LightSalmon);
                                        Graph.DrawLineAa(points_Download[i - 2], points_Download[i - 1], points_Download[i], points_Download[i + 1], Colors.LightSeaGreen, 2);
                                        Graph.FillEllipseCentered(points_Download[i], points_Download[i + 1], 2, 2, Colors.LightSeaGreen);
                                    }));
                            }
                            else
                            {
                                points_Download[i + 1] = ConvToGraphCoords(DownloadSpeed, height);
                                points_Upload[i + 1] = ConvToGraphCoords(UploadSpeed, height);
                                await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                                {
                                    Graph.DrawLineAa(points_Upload[i - 2], points_Upload[i - 1], points_Upload[i], points_Upload[i + 1], Colors.LightSalmon, 2);
                                    Graph.FillEllipseCentered(points_Upload[i], points_Upload[i + 1], 2, 2, Colors.LightSalmon);
                                    Graph.DrawLineAa(points_Download[i - 2], points_Download[i - 1], points_Download[i], points_Download[i + 1], Colors.LightSeaGreen, 2);
                                    Graph.FillEllipseCentered(points_Download[i], points_Download[i + 1], 2, 2, Colors.LightSeaGreen);
                                }));
                                await Task.Delay(1000);
                            }
                        }
                        j++;
                        if (j == 2)
                            j = 0;
                    }
                });
            }
        }

        private int ConvToGraphCoords(ulong value, int height)
        {
            if(value > (ulong)Math.Pow(1024,2))
            {
                return (int)(height * (1.0 / 3.0)) - (int)(((decimal)value / ((decimal)Math.Pow(1024, 3) - ((decimal)Math.Pow(1024, 2)))) * (decimal)40);
            }
            else if(value > (ulong)Math.Pow(1024,1))
            {
                return (int)(height * (2.0 / 3.0)) - (int)(((decimal)value / ((decimal)Math.Pow(1024, 2) - ((decimal)Math.Pow(1024, 1)))) * (decimal)40 );
            }
            else
            {
                return height - (int)((decimal)value / ((decimal)Math.Pow(1024, 1) ) * (decimal)40);
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
