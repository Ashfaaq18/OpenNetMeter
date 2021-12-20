using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WhereIsMyData.Models
{
    public class MyAppInfo : INotifyPropertyChanged
    {
        public string Name { get; set; }

        private ulong dataRecv;
        public ulong DataRecv
        {
            get { return dataRecv; }
            set { dataRecv = value; OnPropertyChanged("DataRecv"); }
        }
        private ulong dataSend;
        public ulong DataSend
        {
            get { return dataSend; }
            set { dataSend = value; OnPropertyChanged("DataSend"); }
        }

        public ImageSource Icon { get; set; }

        public string Image { get; set; }
        public MyAppInfo(string nameP, ulong dataRecvP, ulong dataSendP, System.Drawing.Icon icon)
        {
            Name = nameP;
            DataRecv = dataRecvP;
            DataSend = dataSendP;
            if(icon != null)
            {
                ImageSource im = IconToImgSource.ToImageSource(icon);
                Icon = im;
                im.Freeze();
            }
        }

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
