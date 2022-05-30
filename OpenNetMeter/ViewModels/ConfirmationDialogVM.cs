using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OpenNetMeter.ViewModels
{
    public class ConfirmationDialogVM : INotifyPropertyChanged
    {
        private Visibility isVisible;
        public Visibility IsVisible
        {
            get { return isVisible; }
            set
            {
                isVisible = value;
                OnPropertyChanged("IsVisible");
            }
        }
        public string? ButtonVal { get; set; }

        public ICommand? BtnCommand { get; set; }

        private DataUsageDetailedVM? vmObj;

        public ConfirmationDialogVM()
        {
            IsVisible = Visibility.Hidden;
            //BtnCommand = new BaseCommand(Result);
        }
        //private void Result(object obj)
        //{
        //    if(vmObj != null)
        //        vmObj.GetConfirmBtnVal = obj as string;
        //}

        public void SetVM(in object vm)
        {
            vmObj = vm as DataUsageDetailedVM;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
