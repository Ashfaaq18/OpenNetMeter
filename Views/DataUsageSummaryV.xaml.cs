﻿using System.Diagnostics;
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
        private bool isLoaded = false;
        private DataUsageSummaryVM dusvm;

        private double StartGraphWidth;
        private double StartGraphHeight;
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
                dusvm.Xstart = maxYtextSize.Width;
                //maxYtextSize.Height += 4.0;
                double GraphHeight = Graph.ActualHeight - maxYtextSize.Height;
                double GraphWidth = Graph.ActualWidth - maxYtextSize.Width;
                StartGraphWidth = Graph.ActualWidth;
                StartGraphHeight = Graph.ActualHeight;
                dusvm.GraphWidth = GraphWidth;
                dusvm.GraphHeight = GraphHeight;
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

                resizeTimer.Tick += resizeTimer_Tick;
            };
        }

        private void resizeTimer_Tick(object sender, EventArgs e)
        {
            resizeTimer.IsEnabled = false;

            //Do end of resize processing
            dusvm.pauseDraw = false;
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
                resizeTimer.IsEnabled = true;
                resizeTimer.Stop();
                resizeTimer.Start();

                //Stop drawing graph
                //dusvm.StopDraw();
                dusvm.pauseDraw = true;

                double GraphHeight = Graph.ActualHeight - maxYtextSize.Height;
                double GraphWidth = Graph.ActualWidth - maxYtextSize.Width;
                dusvm.GraphWidth = GraphWidth;
                dusvm.GraphHeight = GraphHeight;
                for (int i = 0; i < dusvm.Lines.Count; i++) //scale the chart
                {
                    dusvm.Lines[i].From = new Point(dusvm.Xstart + dusvm.Points[i].From.X * (GraphWidth / 60.0), dusvm.ConvToGraphCoords((ulong)dusvm.Points[i].From.Y, GraphHeight));
                    dusvm.Lines[i].To = new Point(dusvm.Xstart + dusvm.Points[i].To.X * (GraphWidth / 60.0), dusvm.ConvToGraphCoords((ulong)dusvm.Points[i].To.Y, GraphHeight));
                    //Debug.WriteLine("1: " + dusvm.Points[i].From.Y + " 2: " + dusvm.Points[i].From.Y * heightRatio);
                }

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
        }
       
    }
}
