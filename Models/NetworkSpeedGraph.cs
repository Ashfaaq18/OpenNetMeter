using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WhereIsMyData.Models
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

        public NetworkSpeedGraph(DataUnits downloadSpeed)
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
                (decimal, int) temp2 = DataSizeSuffix.SizeSuffix(temp);
                Yaxis.Add(temp2.Item1.ToString() + DataSizeSuffix.Suffix(temp2.Item2));
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

                int[] points = new int[resolution];
                for (int i = 0; i < points.Length; i++)
                    points[i] = 0;

                Task.Run(async () =>
                {
                    //first init
                    for (int i = 0; i < points.Length; i += 2)
                    {
                        if (i == 0)
                        {
                            points[i] = 0;
                            points[i + 1] = height;
                            await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                            {
                                Graph.DrawLineAa(points[i], points[i + 1], points[i], points[i + 1], Colors.LightSeaGreen, 2);
                                Graph.FillEllipseCentered(points[i], points[i + 1], 2, 2, Colors.LightSeaGreen);
                            }));
                        }
                        else
                        {
                            points[i] = (width / ((resolution / 2) - 1)) * i / 2;

                            //points[i + 1] = new Random().Next(0, height);
                            points[i + 1] = ConvToGraphCoords((double)downloadSpeed.dataValue, downloadSpeed.dataSuffix, height);
                            try
                            {
                                await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    Graph.DrawLineAa(points[i - 2], points[i - 1], points[i], points[i + 1], Colors.LightSeaGreen, 2);
                                    Graph.FillEllipseCentered(points[i], points[i + 1], 2, 2, Colors.LightSeaGreen);
                                }));
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                            }
                            
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
                                    await Application.Current.Dispatcher.BeginInvoke((Action)(() => Xaxis[i] += 30));
                                else
                                    await Application.Current.Dispatcher.BeginInvoke((Action)(() => Xaxis[i] -= 30));
                            }
                            else
                            {
                                if (i < Xaxis.Count / 2)
                                    await Application.Current.Dispatcher.BeginInvoke((Action)(() => Xaxis[i] -= 30));
                                else
                                    await Application.Current.Dispatcher.BeginInvoke((Action)(() => Xaxis[i] += 30));
                            }
                        }


                        //clear old graph
                        await Application.Current.Dispatcher.BeginInvoke((Action)(() => Graph.Clear(Colors.White)));

                        //redraw borders
                        for (int i = 1; i < Xaxis.Count; i++)
                            await Application.Current.Dispatcher.BeginInvoke((Action)(() => Graph.DrawLine(i * width / Xaxis.Count, 0, i * width / Xaxis.Count, height, Colors.LightGray)));

                        for (int i = 1; i < Yaxis.Count; i++)
                            await Application.Current.Dispatcher.BeginInvoke((Action)(() => Graph.DrawLine(0, i * height / Yaxis.Count, width, i * height / Yaxis.Count, Colors.LightGray)));

                        await Application.Current.Dispatcher.BeginInvoke((Action)(() => Graph.DrawRectangle(0, 0, width, height, Colors.Black)));

                        //shift graph halfway back and start plotting new to show continuation
                        for (int i = 0; i < resolution; i += 2)
                        {
                            if (i < resolution / 2 + 1)
                            {
                                points[i + 1] = points[i + 7];
                                points[i + 7] = 0;
                                if (i == 0)
                                {
                                    await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                                    {
                                        Graph.DrawLineAa(points[i], points[i + 1], points[i], points[i + 1], Colors.LightSeaGreen, 2);
                                        Graph.FillEllipseCentered(points[i], points[i + 1], 2, 2, Colors.LightSeaGreen);
                                    }));
                                }
                                else
                                    await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                                    {
                                        Graph.DrawLineAa(points[i - 2], points[i - 1], points[i], points[i + 1], Colors.LightSeaGreen, 2);
                                        Graph.FillEllipseCentered(points[i], points[i + 1], 2, 2, Colors.LightSeaGreen);
                                    }));
                            }
                            else
                            {
                                points[i + 1] = ConvToGraphCoords((double)downloadSpeed.dataValue, downloadSpeed.dataSuffix, height);
                                await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    Graph.DrawLineAa(points[i - 2], points[i - 1], points[i], points[i + 1], Colors.LightSeaGreen, 2);
                                    Graph.FillEllipseCentered(points[i], points[i + 1], 2, 2, Colors.LightSeaGreen);
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

        private int ConvToGraphCoords(double value, int suffix, int height)
        {
            int temp = 0;
            switch (suffix)
            {
                case 0:
                    temp = (int)((double)height - (value / 512.0) * 20.0);
                    //Debug.WriteLine("value: " + value + " suffix: " + suffix + " coordinate: " + temp);
                    return temp;
                case 1:
                    temp = (int)((double)height - (value / 512.0) * 20.0 - 40.0);
                    //Debug.WriteLine("value: " + value + " suffix: " + suffix + " coordinate: " + temp);
                    return temp;
                case 2:
                    temp = (int)((double)height - (value / 512.0) * 20.0 - 80.0);
                    //Debug.WriteLine("value: " + value + " suffix: " + suffix + " coordinate: " + temp);
                    return temp;
                default:
                    return 0;
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
