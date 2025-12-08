using System.Diagnostics;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;
using OpenNetMeter.ViewModels;
using System.Windows;
using System.Collections.Generic;
using System;
using System.Windows.Threading;
using System.Threading;
using OpenNetMeter.Models;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for DataUsageSummaryV.xaml
    /// </summary>
    public partial class DataUsageSummaryV : UserControl
    {
        private DispatcherTimer resizeTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };
        private DataUsageSummaryVM? dusvm;

        //private Rectangle GridBorder;
        private Size maxYtextSize;

        public DataUsageSummaryV()
        {
            InitializeComponent();
            Loaded += delegate
            {
                dusvm = (DataUsageSummaryVM)this.DataContext;

                maxYtextSize = ShapeMeasure(new TextBlock { Text = "0512Mb", FontSize = 11, Padding = new Thickness(0) });
                maxYtextSize.Width += 2.0;
                dusvm.Graph.Xstart = maxYtextSize.Width;
                double GraphHeight = GraphSize.Height;
                double GraphWidth = GraphSize.Width;
                dusvm.Graph.GraphWidth = GraphWidth;
                dusvm.Graph.GraphHeight = GraphHeight;

                resizeTimer.Tick += ResizeTimer_Tick;
                Graph_SizeChanged(null,null);
            };
        }


        private void ResizeTimer_Tick(object? sender, EventArgs e)
        {
            resizeTimer.IsEnabled = false;

            //Do end of resize processing
            if(dusvm != null)
                dusvm.Graph.resumeDraw = true;
        }


        private Size GraphSize => new Size(
            Math.Max(0, GraphGrid.ActualWidth - 16 - maxYtextSize.Width),
            Math.Max(0, GraphGrid.ActualHeight - maxYtextSize.Height));

        public Size ShapeMeasure(TextBlock tb)
        {
            // Measured Size is bounded to be less than maxSize
            Size maxSize = new Size(
                 double.PositiveInfinity,
                 double.PositiveInfinity);
            tb.Measure(maxSize);
            return tb.DesiredSize;
        }

        //scale the MyGraph coordinates
        private void Graph_SizeChanged(object? sender, System.Windows.SizeChangedEventArgs? e)
        {
            if (dusvm != null)
            {
                resizeTimer.IsEnabled = true;
                resizeTimer.Stop();
                resizeTimer.Start();

                //Stop drawing MyGraph
                dusvm.Graph.resumeDraw = false;

                double GraphHeight = GraphSize.Height;
                double GraphWidth = GraphSize.Width;
                dusvm.Graph.GraphWidth = GraphWidth;
                dusvm.Graph.GraphHeight = GraphHeight;

                for (int i = 0; i < dusvm.Graph.DownloadLines.Count; i++) //scale the download line
                {
                    dusvm.Graph.DownloadLines[i].From = new Point(dusvm.Graph.Xstart + dusvm.Graph.DownloadPoints[i].From.X * (GraphWidth / dusvm.Graph.XaxisResolution), dusvm.Graph.ConvToGraphCoords(dusvm.Graph.DownloadPoints[i].From.Y, GraphHeight));
                    dusvm.Graph.DownloadLines[i].To = new Point(dusvm.Graph.Xstart + dusvm.Graph.DownloadPoints[i].To.X * (GraphWidth / dusvm.Graph.XaxisResolution), dusvm.Graph.ConvToGraphCoords(dusvm.Graph.DownloadPoints[i].To.Y, GraphHeight));

                }
                for (int i = 0; i < dusvm.Graph.UploadLines.Count; i++) //scale the upload line
                {
                    dusvm.Graph.UploadLines[i].From = new Point(dusvm.Graph.Xstart + dusvm.Graph.UploadPoints[i].From.X * (GraphWidth / dusvm.Graph.XaxisResolution), dusvm.Graph.ConvToGraphCoords(dusvm.Graph.UploadPoints[i].From.Y, GraphHeight));
                    dusvm.Graph.UploadLines[i].To = new Point(dusvm.Graph.Xstart + dusvm.Graph.UploadPoints[i].To.X * (GraphWidth / dusvm.Graph.XaxisResolution), dusvm.Graph.ConvToGraphCoords(dusvm.Graph.UploadPoints[i].To.Y, GraphHeight));
                }


                //scale the X lines
                for (int i = 0; i < dusvm.Graph.GridXCount; i++)
                {
                    Canvas.SetTop(dusvm.Graph.Ylabels[i], ((GraphHeight / (dusvm.Graph.GridXCount - 1)) * (dusvm.Graph.GridXCount - 1 - i)) - maxYtextSize.Height / 2.0);
                    Size textSize = ShapeMeasure(dusvm.Graph.Ylabels[i]);
                    Canvas.SetLeft(dusvm.Graph.Ylabels[i], maxYtextSize.Width - textSize.Width - 4.0);

                    if (i > 0 && i < dusvm.Graph.GridXCount - 1)
                    {
                        dusvm.Graph.XLines[i - 1].From = new Point(maxYtextSize.Width, (GraphHeight / (dusvm.Graph.GridXCount - 1)) * i);
                        dusvm.Graph.XLines[i - 1].To = new Point(GraphWidth + maxYtextSize.Width, (GraphHeight / (dusvm.Graph.GridXCount - 1)) * i);
                    }
                }

                //scale the Y lines
                for (int i = 0; i < dusvm.Graph.GridYCount; i++)
                {
                    if (i < dusvm.Graph.GridYCount - 1)
                    {
                        Canvas.SetTop(dusvm.Graph.Xlabels[i], GraphHeight);
                        Canvas.SetLeft(dusvm.Graph.Xlabels[i], (GraphWidth / (dusvm.Graph.GridYCount - 1)) * i + maxYtextSize.Width - dusvm.Graph.Xlabels[i].ActualWidth / 2.0);
                    }
                    else
                    {
                        Canvas.SetTop(dusvm.Graph.Xlabels[i], GraphHeight);
                        Canvas.SetLeft(dusvm.Graph.Xlabels[i], GraphWidth + maxYtextSize.Width / 4);
                    }

                    if (i > 0 && i < dusvm.Graph.GridYCount - 1)
                    {
                        dusvm.Graph.YLines[i - 1].From = new Point((GraphWidth / (dusvm.Graph.GridYCount - 1)) * i + maxYtextSize.Width, 0);
                        dusvm.Graph.YLines[i - 1].To = new Point((GraphWidth / (dusvm.Graph.GridYCount - 1)) * i + maxYtextSize.Width, GraphHeight);
                    }
                }

                //scale the border

                //Y lines
                dusvm.Graph.Borders[0].From = new Point(maxYtextSize.Width, 0);
                dusvm.Graph.Borders[0].To = new Point(maxYtextSize.Width, GraphHeight);

                dusvm.Graph.Borders[1].From = new Point((GraphWidth / (dusvm.Graph.GridYCount - 1)) * (dusvm.Graph.GridYCount - 1) + maxYtextSize.Width, 0);
                dusvm.Graph.Borders[1].To = new Point((GraphWidth / (dusvm.Graph.GridYCount - 1)) * (dusvm.Graph.GridYCount - 1) + maxYtextSize.Width, GraphHeight);

                //X lines
                dusvm.Graph.Borders[2].From = new Point(maxYtextSize.Width, 0);
                dusvm.Graph.Borders[2].To = new Point(GraphWidth + maxYtextSize.Width, 0);

                dusvm.Graph.Borders[3].From = new Point(maxYtextSize.Width, GraphHeight);
                dusvm.Graph.Borders[3].To = new Point(GraphWidth + maxYtextSize.Width, GraphHeight);

                dusvm.Graph.firstDrawAfterResume = true;

            }
        }
       
    }
}
