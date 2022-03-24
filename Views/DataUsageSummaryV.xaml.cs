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
        private DataUsageSummaryVM dusvm;

        //private Rectangle GridBorder;
        private Size maxYtextSize;
        const int GridXCount = 7;
        const int GridYCount = 7;
        public DataUsageSummaryV()
        {
            InitializeComponent();
            Loaded += delegate
            {
                dusvm = (DataUsageSummaryVM)this.DataContext;

                maxYtextSize = ShapeMeasure(new TextBlock { Text = "0512Mb", FontSize = 11, Padding = new Thickness(0) });
                maxYtextSize.Width += 2.0;
                dusvm.Xstart = maxYtextSize.Width;
                double GraphHeight = GetGraphSize().Height;
                double GraphWidth = GetGraphSize().Width;
                dusvm.GraphWidth = GraphWidth;
                dusvm.GraphHeight = GraphHeight;

                //GridBorder = new Rectangle { Width = GraphWidth, Height = GraphHeight, Stroke = Brushes.Black, StrokeThickness = 1 };
                //Canvas.SetLeft(GridBorder, maxYtextSize.Width);
               // Canvas.SetTop(GridBorder, 0);
                //CanvasForBorder.Children.Add(GridBorder);

                resizeTimer.Tick += resizeTimer_Tick;
                Graph_SizeChanged(null,null);
            };
        }


        private void resizeTimer_Tick(object sender, EventArgs e)
        {
            resizeTimer.IsEnabled = false;

            //Do end of resize processing
            dusvm.pauseDraw = false;
        }


        private Size GetGraphSize()
        {
            return new Size(GraphGrid.ActualWidth - 16 - maxYtextSize.Width, GraphGrid.ActualHeight - maxYtextSize.Height);
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

        //scale the MyGraph coordinates
        private void Graph_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            if (dusvm != null)
            {
                resizeTimer.IsEnabled = true;
                resizeTimer.Stop();
                resizeTimer.Start();

                //Stop drawing MyGraph
                dusvm.pauseDraw = true;

                double GraphHeight = GetGraphSize().Height;
                double GraphWidth = GetGraphSize().Width;
                dusvm.GraphWidth = GraphWidth;
                dusvm.GraphHeight = GraphHeight;

                for (int i = 0; i < dusvm.MyGraph.DownloadLines.Count; i++) //scale the download line
                {
                    dusvm.MyGraph.DownloadLines[i].From = new Point(dusvm.Xstart + dusvm.MyGraph.DownloadPoints[i].From.X * (GraphWidth / dusvm.XaxisResolution), dusvm.ConvToGraphCoords((ulong)dusvm.MyGraph.DownloadPoints[i].From.Y, GraphHeight));
                    dusvm.MyGraph.DownloadLines[i].To = new Point(dusvm.Xstart + dusvm.MyGraph.DownloadPoints[i].To.X * (GraphWidth / dusvm.XaxisResolution), dusvm.ConvToGraphCoords((ulong)dusvm.MyGraph.DownloadPoints[i].To.Y, GraphHeight));

                }
                for (int i = 0; i < dusvm.MyGraph.UploadLines.Count; i++) //scale the upload line
                {
                    dusvm.MyGraph.UploadLines[i].From = new Point(dusvm.Xstart + dusvm.MyGraph.UploadPoints[i].From.X * (GraphWidth / dusvm.XaxisResolution), dusvm.ConvToGraphCoords((ulong)dusvm.MyGraph.UploadPoints[i].From.Y, GraphHeight));
                    dusvm.MyGraph.UploadLines[i].To = new Point(dusvm.Xstart + dusvm.MyGraph.UploadPoints[i].To.X * (GraphWidth / dusvm.XaxisResolution), dusvm.ConvToGraphCoords((ulong)dusvm.MyGraph.UploadPoints[i].To.Y, GraphHeight));
                }


                //scale the X lines
                for (int i = 0; i < GridXCount; i++)
                {
                    Canvas.SetTop(dusvm.MyGraph.Ylabels[i], ((GraphHeight / (GridXCount - 1)) * (GridXCount - 1 - i)) - maxYtextSize.Height / 2.0);
                    Size textSize = ShapeMeasure(dusvm.MyGraph.Ylabels[i]);
                    Canvas.SetLeft(dusvm.MyGraph.Ylabels[i], maxYtextSize.Width - textSize.Width - 4.0);

                    if (i > 0 && i < GridXCount - 1)
                    {
                        dusvm.MyGraph.XLines[i - 1].From = new Point(maxYtextSize.Width, (GraphHeight / (GridXCount - 1)) * i);
                        dusvm.MyGraph.XLines[i - 1].To = new Point(GraphWidth + maxYtextSize.Width, (GraphHeight / (GridXCount - 1)) * i);
                    }
                }

                //scale the Y lines
                for (int i = 0; i < GridYCount; i++)
                {
                    if (i < GridYCount - 1)
                    {
                        Canvas.SetTop(dusvm.MyGraph.Xlabels[i], GraphHeight);
                        Canvas.SetLeft(dusvm.MyGraph.Xlabels[i], (GraphWidth / (GridYCount - 1)) * i + maxYtextSize.Width - dusvm.MyGraph.Xlabels[i].ActualWidth / 2.0);
                    }
                    else
                    {
                        Canvas.SetTop(dusvm.MyGraph.Xlabels[i], GraphHeight);
                        Canvas.SetLeft(dusvm.MyGraph.Xlabels[i], GraphWidth + maxYtextSize.Width / 4);
                    }

                    if (i > 0 && i < GridYCount - 1)
                    {
                        dusvm.MyGraph.YLines[i - 1].From = new Point((GraphWidth / (GridYCount - 1)) * i + maxYtextSize.Width, 0);
                        dusvm.MyGraph.YLines[i - 1].To = new Point((GraphWidth / (GridYCount - 1)) * i + maxYtextSize.Width, GraphHeight);
                    }
                }

                //scale the border

                //Y lines
                dusvm.MyGraph.Borders[0].From = new Point(maxYtextSize.Width, 0);
                dusvm.MyGraph.Borders[0].To = new Point(maxYtextSize.Width, GraphHeight);

                dusvm.MyGraph.Borders[1].From = new Point((GraphWidth / (GridYCount - 1)) * (GridYCount - 1) + maxYtextSize.Width, 0);
                dusvm.MyGraph.Borders[1].To = new Point((GraphWidth / (GridYCount - 1)) * (GridYCount - 1) + maxYtextSize.Width, GraphHeight);

                //X lines
                dusvm.MyGraph.Borders[2].From = new Point(maxYtextSize.Width, 0);
                dusvm.MyGraph.Borders[2].To = new Point(GraphWidth + maxYtextSize.Width, 0);

                dusvm.MyGraph.Borders[3].From = new Point(maxYtextSize.Width, GraphHeight);
                dusvm.MyGraph.Borders[3].To = new Point(GraphWidth + maxYtextSize.Width, GraphHeight);

            }
        }
       
    }
}
