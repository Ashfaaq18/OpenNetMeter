using System.Diagnostics;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;
using OpenNetMeter.ViewModels;
using System.Windows;
using System.Collections.Generic;
using System;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for DataUsageSummaryV.xaml
    /// </summary>
    public partial class DataUsageSummaryV : UserControl
    {
        private DataUsageSummaryVM dusvm;
        public DataUsageSummaryV()
        {
            InitializeComponent();
            Loaded += delegate
            {
                dusvm = (DataUsageSummaryVM)DataContext;
                Debug.WriteLine(dusvm.TotalUploadData);
                DrawPoints();
            };
        }

        private void LineStuff()
        {
            /*
            double heightResolution = (Graph.ActualHeight / 7.0);
            double fontHeight = LineX1Label.ActualHeight;
            double fontWidth = LineX2Label.ActualWidth;
            //Grid X-axis
            LineX1.X1 = fontWidth + 5.0;
            LineX1.X2 = Graph.ActualWidth;
            LineX1.Y1 = heightResolution / 2.0;
            LineX1.Y2 = heightResolution / 2.0;
            Canvas.SetTop(LineX1Label, LineX1.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX1Label, fontWidth - LineX1Label.ActualWidth);

            LineX2.X1 = LineX1.X1;
            LineX2.X2 = LineX1.X2;
            LineX2.Y1 = heightResolution + LineX1.Y1;
            LineX2.Y2 = heightResolution + LineX1.Y1;
            Canvas.SetTop(LineX2Label, LineX2.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX2Label, fontWidth - LineX2Label.ActualWidth);

            LineX3.X1 = LineX1.X1;
            LineX3.X2 = LineX1.X2;
            LineX3.Y1 = heightResolution + LineX2.Y1;
            LineX3.Y2 = heightResolution + LineX2.Y1;
            Canvas.SetTop(LineX3Label, LineX3.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX3Label, fontWidth - LineX3Label.ActualWidth);

            LineX4.X1 = LineX1.X1;
            LineX4.X2 = LineX1.X2;
            LineX4.Y1 = heightResolution + LineX3.Y1;
            LineX4.Y2 = heightResolution + LineX3.Y1;
            Canvas.SetTop(LineX4Label, LineX4.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX4Label, fontWidth - LineX4Label.ActualWidth);

            LineX5.X1 = LineX1.X1;
            LineX5.X2 = LineX1.X2;
            LineX5.Y1 = heightResolution + LineX4.Y1;
            LineX5.Y2 = heightResolution + LineX4.Y1;
            Canvas.SetTop(LineX5Label, LineX5.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX5Label, fontWidth - LineX5Label.ActualWidth);

            LineX6.X1 = LineX1.X1;
            LineX6.X2 = LineX1.X2;
            LineX6.Y1 = heightResolution + LineX5.Y1;
            LineX6.Y2 = heightResolution + LineX5.Y1;
            Canvas.SetTop(LineX6Label, LineX6.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX6Label, fontWidth - LineX6Label.ActualWidth);

            LineX7.X1 = LineX1.X1;
            LineX7.X2 = LineX1.X2;
            LineX7.Y1 = heightResolution + LineX6.Y1;
            LineX7.Y2 = heightResolution + LineX6.Y1;

            //Grid Y-axis
            LineY1.X1 = LineX1.X1;
            LineY1.X2 = LineX1.X1;
            LineY1.Y1 = LineX1.Y1;
            LineY1.Y2 = Graph.ActualHeight - heightResolution / 2.0;
            Canvas.SetTop(LineY1Label, LineY1.Y2);
            Canvas.SetLeft(LineY1Label, LineY1.X1 - LineY1Label.ActualWidth / 2.0);

            double widthResolution = ((Graph.ActualWidth - LineX1.X1) / 6.0);

            LineY2.X1 = LineX1.X1 + widthResolution;
            LineY2.X2 = LineX1.X1 + widthResolution;
            LineY2.Y1 = LineX1.Y1;
            LineY2.Y2 = LineY1.Y2;
            Canvas.SetTop(LineY2Label, LineY1.Y2);
            Canvas.SetLeft(LineY2Label, LineY2.X1 - LineY2Label.ActualWidth / 2.0);

            LineY3.X1 = LineY2.X1 + widthResolution;
            LineY3.X2 = LineY2.X1 + widthResolution;
            LineY3.Y1 = LineX1.Y1;
            LineY3.Y2 = LineY1.Y2;
            Canvas.SetTop(LineY3Label, LineY1.Y2);
            Canvas.SetLeft(LineY3Label, LineY3.X1 - LineY3Label.ActualWidth / 2.0);

            LineY4.X1 = LineY3.X1 + widthResolution;
            LineY4.X2 = LineY3.X1 + widthResolution;
            LineY4.Y1 = LineX1.Y1;
            LineY4.Y2 = LineY1.Y2;
            Canvas.SetTop(LineY4Label, LineY1.Y2);
            Canvas.SetLeft(LineY4Label, LineY4.X1 - LineY4Label.ActualWidth / 2.0);

            LineY5.X1 = LineY4.X1 + widthResolution;
            LineY5.X2 = LineY4.X1 + widthResolution;
            LineY5.Y1 = LineX1.Y1;
            LineY5.Y2 = LineY1.Y2;
            Canvas.SetTop(LineY5Label, LineY1.Y2);
            Canvas.SetLeft(LineY5Label, LineY5.X1 - LineY5Label.ActualWidth / 2.0);

            LineY6.X1 = LineY5.X1 + widthResolution;
            LineY6.X2 = LineY5.X1 + widthResolution;
            LineY6.Y1 = LineX1.Y1;
            LineY6.Y2 = LineY1.Y2;
            Canvas.SetTop(LineY6Label, LineY1.Y2);
            Canvas.SetLeft(LineY6Label, LineY6.X1 - LineY6Label.ActualWidth / 2.0);

            LineY7.X1 = LineY6.X1 + widthResolution;
            LineY7.X2 = LineY6.X1 + widthResolution;
            LineY7.Y1 = LineX1.Y1;
            LineY7.Y2 = LineY1.Y2;
            Canvas.SetTop(LineY7Label, LineY1.Y2);
            Canvas.SetLeft(LineY7Label, LineY7.X1 - LineY7Label.ActualWidth + 4.0);*/
        }
        private void Graph_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            /*Graph.Children.Clear();
            //Grid boundary
            GridBoundary.Stroke = Brushes.Black;
            GridBoundary.StrokeThickness = 1;
            //Grid Y
            GridLines.Stroke = Brushes.Gray;
            GridLines.StrokeThickness = 1;

            StreamGeometry geometry = new StreamGeometry();

            using (StreamGeometryContext ctx = geometry.Open())
            {
                for (int i = 1; i < 6; i++)
                {
                    //Grid Y lines
                    ctx.BeginFigure(new Point((e.NewSize.Width / 6.0) * i, 0), true, false);
                    ctx.LineTo(new Point((e.NewSize.Width / 6.0) * i, e.NewSize.Height), true, false);

                    //Grid X Lines
                    ctx.BeginFigure(new Point(0, (e.NewSize.Height / 6.0) * i), true, false);
                    ctx.LineTo(new Point(e.NewSize.Width, (e.NewSize.Height / 6.0) * i), true, false);
                }
            }

            geometry.Freeze();
            GridLines.Data = geometry;
            Graph.Children.Add(GridLines);

            geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                //outer border
                ctx.BeginFigure(new Point(0, 0), true, true);
                List<Point> points = new List<Point>() {
                    new Point(e.NewSize.Width, 0),
                    new Point(e.NewSize.Width, e.NewSize.Height),
                    new Point(0, e.NewSize.Height)
                };
                ctx.PolyLineTo(points, true, true);
            }

            geometry.Freeze();
            GridBoundary.Data = geometry;

            Graph.Children.Add(GridBoundary);*/

           double heightResolution = (Graph.ActualHeight / 7.0);
           double fontHeight = LineX1Label.ActualHeight;
           double fontWidth = LineX2Label.ActualWidth;
           //Grid X-axis
           LineX1.X1 = fontWidth + 5.0;
           LineX1.X2 = Graph.ActualWidth;
           LineX1.Y1 = heightResolution / 2.0;
           LineX1.Y2 = heightResolution / 2.0;
           Canvas.SetTop(LineX1Label, LineX1.Y1 - fontHeight / 2.0);
           Canvas.SetLeft(LineX1Label, fontWidth - LineX1Label.ActualWidth);

           LineX2.X1 = LineX1.X1;
           LineX2.X2 = LineX1.X2;
           LineX2.Y1 = heightResolution + LineX1.Y1;
           LineX2.Y2 = heightResolution + LineX1.Y1;
           Canvas.SetTop(LineX2Label, LineX2.Y1 - fontHeight / 2.0);
           Canvas.SetLeft(LineX2Label, fontWidth - LineX2Label.ActualWidth);

           LineX3.X1 = LineX1.X1;
           LineX3.X2 = LineX1.X2;
           LineX3.Y1 = heightResolution + LineX2.Y1;
           LineX3.Y2 = heightResolution + LineX2.Y1;
           Canvas.SetTop(LineX3Label, LineX3.Y1 - fontHeight / 2.0);
           Canvas.SetLeft(LineX3Label, fontWidth - LineX3Label.ActualWidth);

           LineX4.X1 = LineX1.X1;
           LineX4.X2 = LineX1.X2;
           LineX4.Y1 = heightResolution + LineX3.Y1;
           LineX4.Y2 = heightResolution + LineX3.Y1;
           Canvas.SetTop(LineX4Label, LineX4.Y1 - fontHeight / 2.0);
           Canvas.SetLeft(LineX4Label, fontWidth - LineX4Label.ActualWidth);

           LineX5.X1 = LineX1.X1;
           LineX5.X2 = LineX1.X2;
           LineX5.Y1 = heightResolution + LineX4.Y1;
           LineX5.Y2 = heightResolution + LineX4.Y1;
           Canvas.SetTop(LineX5Label, LineX5.Y1 - fontHeight / 2.0);
           Canvas.SetLeft(LineX5Label, fontWidth - LineX5Label.ActualWidth);

           LineX6.X1 = LineX1.X1;
           LineX6.X2 = LineX1.X2;
           LineX6.Y1 = heightResolution + LineX5.Y1;
           LineX6.Y2 = heightResolution + LineX5.Y1;
           Canvas.SetTop(LineX6Label, LineX6.Y1 - fontHeight / 2.0);
           Canvas.SetLeft(LineX6Label, fontWidth - LineX6Label.ActualWidth);

           LineX7.X1 = LineX1.X1;
           LineX7.X2 = LineX1.X2;
           LineX7.Y1 = heightResolution + LineX6.Y1;
           LineX7.Y2 = heightResolution + LineX6.Y1;

           //Grid Y-axis
           LineY1.X1 = LineX1.X1;
           LineY1.X2 = LineX1.X1;
           LineY1.Y1 = LineX1.Y1;
           LineY1.Y2 = Graph.ActualHeight - heightResolution / 2.0;
           Canvas.SetTop(LineY1Label, LineY1.Y2);
           Canvas.SetLeft(LineY1Label, LineY1.X1 - LineY1Label.ActualWidth / 2.0);

           double widthResolution = ((Graph.ActualWidth - LineX1.X1) / 6.0);

           LineY2.X1 = LineX1.X1 + widthResolution;
           LineY2.X2 = LineX1.X1 + widthResolution;
           LineY2.Y1 = LineX1.Y1;
           LineY2.Y2 = LineY1.Y2;
           Canvas.SetTop(LineY2Label, LineY1.Y2);
           Canvas.SetLeft(LineY2Label, LineY2.X1 - LineY2Label.ActualWidth / 2.0);

           LineY3.X1 = LineY2.X1 + widthResolution;
           LineY3.X2 = LineY2.X1 + widthResolution;
           LineY3.Y1 = LineX1.Y1;
           LineY3.Y2 = LineY1.Y2;
           Canvas.SetTop(LineY3Label, LineY1.Y2);
           Canvas.SetLeft(LineY3Label, LineY3.X1 - LineY3Label.ActualWidth / 2.0);

           LineY4.X1 = LineY3.X1 + widthResolution;
           LineY4.X2 = LineY3.X1 + widthResolution;
           LineY4.Y1 = LineX1.Y1;
           LineY4.Y2 = LineY1.Y2;
           Canvas.SetTop(LineY4Label, LineY1.Y2);
           Canvas.SetLeft(LineY4Label, LineY4.X1 - LineY4Label.ActualWidth / 2.0);

           LineY5.X1 = LineY4.X1 + widthResolution;
           LineY5.X2 = LineY4.X1 + widthResolution;
           LineY5.Y1 = LineX1.Y1;
           LineY5.Y2 = LineY1.Y2;
           Canvas.SetTop(LineY5Label, LineY1.Y2);
           Canvas.SetLeft(LineY5Label, LineY5.X1 - LineY5Label.ActualWidth / 2.0);

           LineY6.X1 = LineY5.X1 + widthResolution;
           LineY6.X2 = LineY5.X1 + widthResolution;
           LineY6.Y1 = LineX1.Y1;
           LineY6.Y2 = LineY1.Y2;
           Canvas.SetTop(LineY6Label, LineY1.Y2);
           Canvas.SetLeft(LineY6Label, LineY6.X1 - LineY6Label.ActualWidth / 2.0);

           LineY7.X1 = LineY6.X1 + widthResolution;
           LineY7.X2 = LineY6.X1 + widthResolution;
           LineY7.Y1 = LineX1.Y1;
           LineY7.Y2 = LineY1.Y2;
           Canvas.SetTop(LineY7Label, LineY1.Y2);
           Canvas.SetLeft(LineY7Label, LineY7.X1 - LineY7Label.ActualWidth + 4.0);
        }

        private void DrawPoints()
        {
            Task.Run(async () =>
            {
                int i = 0;
                while(true)
                {
                    await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                    {
                        double temp = ConvToGraphCoords(dusvm.tpvm.DownloadSpeed, Graph.ActualHeight - (Graph.ActualHeight / 7.0) / 2.0)   ;
                        Debug.WriteLine( "Graphcoords: " + temp + " speed: " + dusvm.tpvm.DownloadSpeed + " height: " + Graph.ActualHeight + " resolution: " + (Graph.ActualHeight / 7.0));
                        DownloadPolyPath.Points.Add(new Point( (LineX2Label.ActualWidth + 5.0) + (((Graph.ActualWidth - LineX2Label.ActualWidth + 5.0) / 6.0) / 10.0)*i, ConvToGraphCoords(dusvm.tpvm.DownloadSpeed, (Graph.ActualHeight / 7.0) * 6.0) + (Graph.ActualHeight / 7.0) / 2.0));
                    }));
                    await Task.Delay(1000);
                    i++;
                    if (i >= 60)
                    {
                        i = 0;
                        await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                        {
                            DownloadPolyPath.Points.Clear();
                        }));    
                    }
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
    }
}
