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
        }

        public void SetVM(in object vm)
        {
            vmObj = vm as DataUsageDetailedVM;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
