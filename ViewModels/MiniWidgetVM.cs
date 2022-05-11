using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace OpenNetMeter.ViewModels
{
    public class MiniWidgetVM : INotifyPropertyChanged
    {
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

        public ulong downloadSpeed;
        public ulong DownloadSpeed
        {
            get { return downloadSpeed; }
            set
            {
                downloadSpeed = value;
                OnPropertyChanged("DownloadSpeed");
            }
        }
        public ulong uploadSpeed;
        public ulong UploadSpeed
        {
            get { return uploadSpeed; }
            set
            {
                uploadSpeed = value;
                OnPropertyChanged("UploadSpeed");
            }
        }

        private double width;
        public double Width
        {
            get { return width; }
            set
            {
                width = value;
                OnPropertyChanged("Width");
            }
        }

        private double height;
        public double Height
        {
            get { return height; }
            set
            {
                height = value;
                OnPropertyChanged("Height");
            }
        }


        public MiniWidgetVM()
        {
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            DownloadSpeed = 0;
            UploadSpeed = 0;

            Size size1 = ShapeMeasure(new TextBlock { Text = "D-S :", FontSize = 12, Padding = new Thickness(0) });
            Size size2 = ShapeMeasure(new TextBlock { Text = "1024.00Mbps", FontSize = 12, Padding = new Thickness(5,0,0,0) });
            int widthMargins = 5 + 5; //these are from the miniwidget xaml margins
            Width = size1.Width + size2.Width + widthMargins;
            int heightMargins = 2 + 2; //these are from the miniwidget xaml margins
            Height = size1.Height * 2 + heightMargins * 2;
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
