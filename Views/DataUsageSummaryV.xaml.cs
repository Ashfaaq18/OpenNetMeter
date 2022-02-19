using System.Windows.Controls;
using OpenNetMeter.ViewModels;
namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for DataUsageSummaryV.xaml
    /// </summary>
    public partial class DataUsageSummaryV : UserControl
    {
        public DataUsageSummaryV()
        {
            InitializeComponent();
            SizeChanged += DataUsageSummaryV_SizeChanged;
        }

        private void DataUsageSummaryV_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
        {
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

            LineX2.X1 = fontWidth + 5.0;
            LineX2.X2 = Graph.ActualWidth;
            LineX2.Y1 = heightResolution * 1 + heightResolution / 2.0;
            LineX2.Y2 = heightResolution * 1 + heightResolution / 2.0;
            Canvas.SetTop(LineX2Label, LineX2.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX2Label, fontWidth - LineX2Label.ActualWidth);

            LineX3.X1 = fontWidth + 5.0;
            LineX3.X2 = Graph.ActualWidth;
            LineX3.Y1 = heightResolution * 2 + heightResolution / 2.0;
            LineX3.Y2 = heightResolution * 2 + heightResolution / 2.0;
            Canvas.SetTop(LineX3Label, LineX3.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX3Label, fontWidth - LineX3Label.ActualWidth);

            LineX4.X1 = fontWidth + 5.0;
            LineX4.X2 = Graph.ActualWidth;
            LineX4.Y1 = heightResolution* 3 + heightResolution / 2.0;
            LineX4.Y2 = heightResolution* 3 + heightResolution / 2.0;
            Canvas.SetTop(LineX4Label, LineX4.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX4Label, fontWidth - LineX4Label.ActualWidth);

            LineX5.X1 = fontWidth + 5.0;
            LineX5.X2 = Graph.ActualWidth;
            LineX5.Y1 = heightResolution* 4 + heightResolution / 2.0;
            LineX5.Y2 = heightResolution* 4 + heightResolution / 2.0;
            Canvas.SetTop(LineX5Label, LineX5.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX5Label, fontWidth - LineX5Label.ActualWidth);

            LineX6.X1 = fontWidth + 5.0;
            LineX6.X2 = Graph.ActualWidth;
            LineX6.Y1 = heightResolution* 5 + heightResolution / 2.0;
            LineX6.Y2 = heightResolution* 5 + heightResolution / 2.0;
            Canvas.SetTop(LineX6Label, LineX6.Y1 - fontHeight / 2.0);
            Canvas.SetLeft(LineX6Label, fontWidth - LineX6Label.ActualWidth);

            LineX7.X1 = fontWidth + 5.0;
            LineX7.X2 = Graph.ActualWidth;
            LineX7.Y1 = heightResolution* 6 + heightResolution / 2.0;
            LineX7.Y2 = heightResolution* 6 + heightResolution / 2.0;

            //Grid Y-axis
            LineY1.X1 = LineX1.X1;
            LineY1.X2 = LineX1.X1;
            LineY1.Y2 = Graph.ActualHeight;
            Canvas.SetTop(LineY1Label, LineY1.Y2);
            Canvas.SetLeft(LineY1Label, LineY1.X1 - LineY1Label.ActualWidth / 2.0);

            LineY2.X1 = LineX1.X1 + ( (Graph.ActualWidth - LineX1.X1) / 6.0) * 1;
            LineY2.X2 = LineX1.X1 + ( (Graph.ActualWidth - LineX1.X1) / 6.0) * 1;
            LineY2.Y2 = Graph.ActualHeight;
            Canvas.SetTop(LineY2Label, LineY1.Y2);
            Canvas.SetLeft(LineY2Label, LineY2.X1 - LineY2Label.ActualWidth / 2.0);

            LineY3.X1 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 2;
            LineY3.X2 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 2;
            LineY3.Y2 = Graph.ActualHeight;
            Canvas.SetTop(LineY3Label, LineY1.Y2);
            Canvas.SetLeft(LineY3Label, LineY3.X1 - LineY3Label.ActualWidth / 2.0);

            LineY4.X1 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 3;
            LineY4.X2 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 3;
            LineY4.Y2 = Graph.ActualHeight;
            Canvas.SetTop(LineY4Label, LineY1.Y2);
            Canvas.SetLeft(LineY4Label, LineY4.X1 - LineY4Label.ActualWidth / 2.0);

            LineY5.X1 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 4;
            LineY5.X2 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 4;
            LineY5.Y2 = Graph.ActualHeight;
            Canvas.SetTop(LineY5Label, LineY1.Y2);
            Canvas.SetLeft(LineY5Label, LineY5.X1 - LineY5Label.ActualWidth / 2.0);

            LineY6.X1 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 5;
            LineY6.X2 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 5;
            LineY6.Y2 = Graph.ActualHeight;
            Canvas.SetTop(LineY6Label, LineY1.Y2);
            Canvas.SetLeft(LineY6Label, LineY6.X1 - LineY6Label.ActualWidth / 2.0);

            LineY7.X1 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 6;
            LineY7.X2 = LineX1.X1 + ((Graph.ActualWidth - LineX1.X1) / 6.0) * 6;
            LineY7.Y2 = Graph.ActualHeight;
            Canvas.SetTop(LineY7Label, LineY1.Y2);
            Canvas.SetLeft(LineY7Label, LineY7.X1 - LineY7Label.ActualWidth / 2.0);

        }

    }
}
