using OpenNetMeter.Utilities;
using System.ComponentModel;
using System.Windows.Media;

namespace OpenNetMeter.Models
{
    public class MyProcess : INotifyPropertyChanged
    {
        private string? name;
        public string? Name
        {
            get { return name; }
            set { name = value; OnPropertyChanged("Name"); }
        }

        private ulong currentdataRecv;
        public ulong CurrentDataRecv
        {
            get { return currentdataRecv; }
            set { currentdataRecv = value; OnPropertyChanged("CurrentDataRecv"); }
        }

        private ulong currentdataSend;
        public ulong CurrentDataSend
        {
            get { return currentdataSend; }
            set { currentdataSend = value; OnPropertyChanged("CurrentDataSend"); }
        }

        private ulong totaldataRecv;
        public ulong TotalDataRecv
        {
            get { return totaldataRecv; }
            set { totaldataRecv = value; OnPropertyChanged("TotalDataRecv"); }
        }
        private ulong totaldataSend;
        public ulong TotalDataSend
        {
            get { return totaldataSend; }
            set { totaldataSend = value; OnPropertyChanged("TotalDataSend"); }
        }

        public ImageSource? Icon { get; set; }

        public string Image { get; set; }
        public MyProcess(string nameP, ulong dataRecvP, ulong dataSendP, System.Drawing.Icon? icon)
        {
            Name = nameP;
            TotalDataRecv = dataRecvP;
            TotalDataSend = dataSendP;
            CurrentDataRecv = 0;
            CurrentDataSend = 0;
            Icon = null;
            Image = "";
            if(icon != null)
            {
                ImageSource im = IconToImgSource.ToImageSource(icon);
                Icon = im;
                im.Freeze();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
