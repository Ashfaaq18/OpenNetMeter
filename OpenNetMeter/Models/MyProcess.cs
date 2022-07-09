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

        public MyProcess_Small(string nameP, long currentDataRecvP, long currentDataSendP)
        {
            Name = nameP;
            CurrentDataRecv = currentDataRecvP;
            CurrentDataSend = currentDataSendP;
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

        //public ImageSource? Icon { get; set; }

        //public string Image { get; set; }
        public MyProcess_Big(string nameP, long currentDataRecvP, long currentDataSendP, long totalDataRecvP, long totalDataSendP)
        {
            Name = nameP;
            CurrentDataRecv = currentDataRecvP;
            CurrentDataSend = currentDataSendP;
            TotalDataRecv = totalDataRecvP;
            TotalDataSend = totalDataSendP;
            //Icon = null;
            //Image = "";
            //if(icon != null)
            //{
            //    ImageSource im = IconToImgSource.ToImageSource(icon);
            //    Icon = im;
            //    im.Freeze();
            //}
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
