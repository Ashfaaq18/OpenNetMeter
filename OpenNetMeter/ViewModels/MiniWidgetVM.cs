using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace OpenNetMeter.ViewModels
{
    public class MiniWidgetVM : INotifyPropertyChanged
    {
        private long currentSessionDownloadData;
        public long CurrentSessionDownloadData
        {
            get { return currentSessionDownloadData; }
            set
            {
                currentSessionDownloadData = value;
                OnPropertyChanged("CurrentSessionDownloadData");
            }
        }
        private long currentSessionUploadData;
        public long CurrentSessionUploadData
        {
            get { return currentSessionUploadData; }
            set
            {
                currentSessionUploadData = value;
                OnPropertyChanged("CurrentSessionUploadData");
            }
        }

        public long downloadSpeed;
        public long DownloadSpeed
        {
            get { return downloadSpeed; }
            set
            {
                downloadSpeed = value;
                OnPropertyChanged("DownloadSpeed");
            }
        }
        public long uploadSpeed;
        public long UploadSpeed
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

        public string? backgroundColor;
        public string? BackgroundColor
        {
            get { return backgroundColor; }
            set
            {
                backgroundColor = value;
                OnPropertyChanged("BackgroundColor");
            }
        }

        private bool isPinned;
        public bool IsPinned
        {
            get { return isPinned; }
            set
            {
                if (isPinned == value)
                {
                    return;
                }

                isPinned = value;
                SettingsManager.Current.MiniWidgetPinned = value;
                SettingsManager.Save();
                OnPropertyChanged("IsPinned");
            }
        }
        public MiniWidgetVM()
        {
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            DownloadSpeed = 0;
            UploadSpeed = 0;
            BackgroundColor = "#ff212121";

            Size size1 = UIMeasure.Shape(new TextBlock { Text = "D-S :", FontSize = 12, Padding = new Thickness(0) });
            Size size2 = UIMeasure.Shape(new TextBlock { Text = "1024.00Mbps", FontSize = 12, Padding = new Thickness(5,0,0,0) });
            int widthMargins = 5 + 5; //these are from the miniwidget xaml margins
            double pinButtonWidth = 30; //reserve space for the pin toggle button
            Width = size1.Width + size2.Width + widthMargins + pinButtonWidth;
            int heightMargins = 2 + 2; //these are from the miniwidget xaml margins
            Height = size1.Height * 2 + heightMargins * 2;

            isPinned = SettingsManager.Current.MiniWidgetPinned;
            OnPropertyChanged("IsPinned");
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}



