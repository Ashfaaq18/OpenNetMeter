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

        private long currentdataRecv;
        public long CurrentDataRecv
        {
            get { return currentdataRecv; }
            set { currentdataRecv = value; OnPropertyChanged("CurrentDataRecv"); }
        }

        private long currentdataSend;
        public long CurrentDataSend
        {
            get { return currentdataSend; }
            set { currentdataSend = value; OnPropertyChanged("CurrentDataSend"); }
        }

        public ImageSource? Icon { get; set; }

        public string Image { get; set; }
        public MyProcess(string nameP, long dataRecvP, long dataSendP, System.Drawing.Icon? icon)
        {
            Name = nameP;
            CurrentDataRecv = dataRecvP;
            CurrentDataSend = dataSendP;
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
