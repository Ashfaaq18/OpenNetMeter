using OpenNetMeter.Utilities;
using System.ComponentModel;
using System.Windows.Media;

namespace OpenNetMeter.Models
{
    public class MyProcess_Small
    {
        public string? Name { get; set; }

        public long CurrentDataRecv { get; set; }

        public long CurrentDataSend { get; set; }

        public ImageSource? Icon { get; set; }

        public MyProcess_Small(string nameP, long currentDataRecvP, long currentDataSendP, ImageSource? icon = null)
        {
            Name = nameP;
            CurrentDataRecv = currentDataRecvP;
            CurrentDataSend = currentDataSendP;
            Icon = icon;
        }
    }


    public class MyProcess_Big : INotifyPropertyChanged
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
            set 
            { 
                if(currentdataRecv != value)
                {
                    currentdataRecv = value;
                    OnPropertyChanged("CurrentDataRecv");
                }
            }
        }

        private long currentdataSend;
        public long CurrentDataSend
        {
            get { return currentdataSend; }
            set 
            {
                if(currentdataSend != value)
                {
                    currentdataSend = value;
                    OnPropertyChanged("CurrentDataSend");
                }
            }
        }

        private long totaldataRecv;
        public long TotalDataRecv
        {
            get { return totaldataRecv; }
            set 
            { 
                if(totaldataRecv != value)
                {
                    totaldataRecv = value;
                    OnPropertyChanged("TotalDataRecv");
                }
            }
        }

        private long totaldataSend;
        public long TotalDataSend
        {
            get { return totaldataSend; }
            set 
            { 
                if(totaldataSend != value)
                {
                    totaldataSend = value;
                    OnPropertyChanged("TotalDataSend");
                }
            }
        }

        private ImageSource? icon;
        public ImageSource? Icon
        {
            get { return icon; }
            set
            {
                if (icon != value)
                {
                    icon = value;
                    OnPropertyChanged(nameof(Icon));
                }
            }
        }
        
        public MyProcess_Big(string nameP, long currentDataRecvP, long currentDataSendP, long totalDataRecvP, long totalDataSendP, ImageSource? iconP = null)
        {
            Name = nameP;
            CurrentDataRecv = currentDataRecvP;
            CurrentDataSend = currentDataSendP;
            TotalDataRecv = totalDataRecvP;
            TotalDataSend = totalDataSendP;
            Icon = iconP;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
