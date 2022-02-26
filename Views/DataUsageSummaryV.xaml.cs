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
        private bool isLoaded = false;
        private DataUsageSummaryVM dusvm;

        private List<TextBlock> Xlabels;
        private List<TextBlock> Ylabels;
        private List<Line> GridXLines;
        private List<Line> GridYLines;
        private Rectangle GridBorder;
        private Size maxYtextSize;
        const int GridXCount = 7;
        const int GridYCount = 7;
        public DataUsageSummaryV()
        {
            InitializeComponent();
            Loaded += delegate
            {
                dusvm = (DataUsageSummaryVM)this.DataContext;
                isLoaded = true;

                Ylabels = new List<TextBlock>();
                Xlabels = new List<TextBlock>();
                GridXLines = new List<Line>();
                GridYLines = new List<Line>();
                ulong temp = 512;
                maxYtextSize = ShapeMeasure(new TextBlock { Text = "0512Mb", FontSize = 11, Padding = new Thickness(0) });
                maxYtextSize.Width += 4.0;
                //maxYtextSize.Height += 4.0;
                double GraphHeight = Graph.ActualHeight - maxYtextSize.Height;
                double GraphWidth = Graph.ActualWidth - maxYtextSize.Width;
                for (int i = 0; i < GridXCount - 2; i++)
                {
                    if (i != 0)
                    {
                        if (i % 2 == 0)
                            temp *= 512;
                        else
                            temp *= 2;
                    }
                    Ylabels.Add(new TextBlock { Text = DataSizeSuffix.SizeSuffixInStr(temp, 1, false), FontSize = 11, Padding = new Thickness(0,0,4,0) });
                    Size textSize = ShapeMeasure(Ylabels[i]);
                    Canvas.SetLeft(Ylabels[i], maxYtextSize.Width - textSize.Width);
                    Canvas.SetTop(Ylabels[i], ( (GraphHeight / (GridXCount - 1)) * ( GridXCount - i -2)) - textSize.Height/2.0);

                    GridXLines.Add(new Line { X1 = maxYtextSize.Width, Y1 = (GraphHeight / (GridXCount - 1)) * (i + 1), 
                        X2 = Graph.ActualWidth, Y2 = (GraphHeight / (GridXCount - 1)) * (i + 1), Stroke = Brushes.LightGray , StrokeThickness = 1 });
                    
                    Graph.Children.Add(Ylabels[i]);
                    Graph.Children.Add(GridXLines[i]);
                }

                for (int i = 0; i < GridYCount - 2; i++)
                {
                    Xlabels.Add(new TextBlock { Text = ((i+1)*10).ToString(), FontSize = 11, Padding = new Thickness(0) });
                    Canvas.SetTop(Xlabels[i], GraphHeight);
                    Canvas.SetLeft(Xlabels[i], (GraphWidth / (GridYCount - 1)) * (i + 1) + maxYtextSize.Width - ShapeMeasure(Xlabels[i]).Width/2.0);
                    GridYLines.Add(new Line { X1 = (GraphWidth / (GridYCount - 1)) * (i + 1) + maxYtextSize.Width, Y1 = 0, 
                        X2 = (GraphWidth / (GridYCount - 1)) * (i + 1) + maxYtextSize.Width, Y2 = GraphHeight, Stroke = Brushes.LightGray, StrokeThickness = 1 });
                    Graph.Children.Add(Xlabels[i]);
                    Graph.Children.Add(GridYLines[i]);
                }
                GridBorder = new Rectangle { Width = GraphWidth, Height = GraphHeight, Stroke = Brushes.Black, StrokeThickness = 1 };
                Canvas.SetLeft(GridBorder, maxYtextSize.Width);
                Graph.Children.Add(GridBorder);
            };
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
        //scale the graph coordinates
        private void Graph_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
            if(isLoaded)
            {
                double widthRatio = e.NewSize.Width / e.PreviousSize.Width;
                double heightRatio = e.NewSize.Height / e.PreviousSize.Height;
                double GraphHeight = Graph.ActualHeight - maxYtextSize.Height;
                double GraphWidth = Graph.ActualWidth - maxYtextSize.Width;
                for (int i = 0; i < GridXCount-2; i++)
                {
                    Canvas.SetTop(Ylabels[i], ((GraphHeight / (GridXCount - 1)) * (GridXCount - i - 2)) - maxYtextSize.Height / 2.0);

                    GridXLines[i].X1 = maxYtextSize.Width;
                    GridXLines[i].Y1 = (GraphHeight / (GridXCount - 1)) * (i + 1);

                    GridXLines[i].X2 = Graph.ActualWidth;
                    GridXLines[i].Y2 = (GraphHeight / (GridXCount - 1)) * (i + 1);
                }

                for (int i = 0; i < GridYCount-2; i++)
                {
                    Canvas.SetTop(Xlabels[i], GraphHeight);
                    Canvas.SetLeft(Xlabels[i], (GraphWidth / (GridYCount - 1)) * (i + 1) + maxYtextSize.Width - Xlabels[i].ActualWidth / 2.0);

                    GridYLines[i].X1 = (GraphWidth / (GridYCount - 1)) * (i + 1) + maxYtextSize.Width;
                    GridYLines[i].Y1 = 0;

                    GridYLines[i].X2 = (GraphWidth / (GridYCount - 1)) * (i + 1) + maxYtextSize.Width;
                    GridYLines[i].Y2 = GraphHeight;
                }

                GridBorder.Width = GraphWidth;
                GridBorder.Height = GraphHeight;
            }
            
            /*
            //set size change ratio
            if (isLoaded)
            {
                dusvm.GraphWidth = LineY7.X1 - LineY1.X1;
                dusvm.GraphHeight = LineX7.Y1 - LineX1.Y1;
                //Debug.WriteLine("Width: " + dusvm.GraphWidthRatio + " Height: " + dusvm.GraphHeightRatio);

                for (int i = 0; i < dusvm.Lines.Count; i++)
                {
                    dusvm.Lines[i].From = new Point(dusvm.Xstart + dusvm.Points[i].From.X * (dusvm.GraphWidth / dusvm.StartGraphWidth), dusvm.Points[i].From.Y * (dusvm.GraphHeight / dusvm.StartGraphHeight));
                    dusvm.Lines[i].To = new Point(dusvm.Xstart + dusvm.Points[i].To.X * (dusvm.GraphWidth / dusvm.StartGraphWidth), dusvm.Points[i].To.Y * (dusvm.GraphHeight / dusvm.StartGraphHeight));
                }
            }*/
        }
        /*
        private void DrawPoints()
        {

            cts = new CancellationTokenSource();
            ct = cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Operation Started : draw");
                    int i = 0;
                    while (!ct.IsCancellationRequested)
                    {
                        await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                        {
                            //double temp = ConvToGraphCoords(dusvm.tpvm.DownloadSpeed, Graph.ActualHeight - (Graph.ActualHeight / 7.0) / 2.0)   ;
                            //Debug.WriteLine( "Graphcoords: " + temp + " speed: " + dusvm.tpvm.DownloadSpeed + " height: " + Graph.ActualHeight + " resolution: " + (Graph.ActualHeight / 7.0));
                            //DownloadPolyPath.Points.Add(new Point( (LineX2Label.ActualWidth + 5.0) + (((Graph.ActualWidth - LineX2Label.ActualWidth + 5.0) / 6.0) / 10.0)*i, ConvToGraphCoords(dusvm.tpvm.DownloadSpeed, (Graph.ActualHeight / 7.0) * 6.0) + (Graph.ActualHeight / 7.0) / 2.0));
                            DownloadPolyPath.Points.Add(new Point((LineX2Label.ActualWidth + 5.0) + Graph.ActualWidth / 60 * i, Graph.ActualHeight/2));
                        }));
                        //await Task.Delay(1000);
                        i++;
                        if (i >= 60)
                        {
                            i = 0;
                            await Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
                            {
                                DownloadPolyPath.Points.Clear();
                            }));
                        }
                        await Task.Delay(1000, ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation Cancelled : draw");
                    if (cts != null)
                    {
                        cts.Dispose();
                        cts = null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
                }
            });
        }
        */
       
    }
}
