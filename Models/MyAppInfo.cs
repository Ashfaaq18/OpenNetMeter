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

        public ImageSource Icon { get; set; }

        public string Image { get; set; }
        public MyAppInfo(string name, ulong data, System.Drawing.Icon icon)
        {
            Name = name;
            DataRecv = data;
            ImageSource im = IconToImgSource.ToImageSource(icon);
            Icon = im;
            im.Freeze();
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
