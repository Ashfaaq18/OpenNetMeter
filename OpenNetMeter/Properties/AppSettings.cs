using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace OpenNetMeter.Properties
{
    public class AppSettings : INotifyPropertyChanged
    {

        private bool darkMode;
        public bool DarkMode { get => darkMode; set { if (darkMode != value) { darkMode = value; OnPropertyChanged("DarkMode"); } } }

        private bool startWithWin;
        public bool StartWithWin { get => startWithWin; set { if (startWithWin != value) { startWithWin = value; OnPropertyChanged("StartWithWin"); } } }

        private bool minimizeOnStart = true;
        public bool MinimizeOnStart { get => minimizeOnStart; set { if (minimizeOnStart != value) { minimizeOnStart = value; OnPropertyChanged("MinimizeOnStart"); } } }

        private int networkType = 2;
        public int NetworkType { get => networkType; set { if (networkType != value) { networkType = value; OnPropertyChanged("NetworkType"); } } }

        private Point winPos;
        public Point WinPos { get => winPos; set { if (winPos != value) { winPos = value; OnPropertyChanged("WinPos"); } } }

        private Size winSize;
        public Size WinSize { get => winSize; set { if (winSize != value) { winSize = value; OnPropertyChanged("WinSize"); } } }

        private bool launchFirstTime = true;
        public bool LaunchFirstTime { get => launchFirstTime; set { if (launchFirstTime != value) { launchFirstTime = value; OnPropertyChanged("LaunchFirstTime"); } } }

        private int launchPage;
        public int LaunchPage { get => launchPage; set { if (launchPage != value) { launchPage = value; OnPropertyChanged("LaunchPage"); } } }

        private Point miniWidgetPos;
        public Point MiniWidgetPos { get => miniWidgetPos; set { if (miniWidgetPos != value) { miniWidgetPos = value; OnPropertyChanged("MiniWidgetPos"); } } }

        private bool miniWidgetVisibility;
        public bool MiniWidgetVisibility { get => miniWidgetVisibility; set { if (miniWidgetVisibility != value) { miniWidgetVisibility = value; OnPropertyChanged("MiniWidgetVisibility"); } } }

        private int networkSpeedFormat;
        public int NetworkSpeedFormat { get => networkSpeedFormat; set { if (networkSpeedFormat != value) { networkSpeedFormat = value; OnPropertyChanged("NetworkSpeedFormat"); } } }

        private int miniWidgetTransparentSlider;
        public int MiniWidgetTransparentSlider { get => miniWidgetTransparentSlider; set { if (miniWidgetTransparentSlider != value) { miniWidgetTransparentSlider = value; OnPropertyChanged("MiniWidgetTransparentSlider"); } } }

        private string folder =Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), Assembly.GetEntryAssembly()?.GetName().Name ?? "OpenNetMeter");
        public string Folder { get => folder; set { if (folder != value) { folder = value; OnPropertyChanged("Folder"); } } }


        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

    }
}
