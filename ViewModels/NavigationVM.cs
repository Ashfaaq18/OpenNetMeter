using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WhereIsMyData.ViewModels
{
    class NavigationVM : INotifyPropertyChanged
    {
        public ICommand DataUsageSumCommand { get; set; }

        public ICommand DataUsageDetCommand { get; set; }

        private object selectedViewModel;

        public object SelectedViewModel
        {
            get { return selectedViewModel; }

            set { selectedViewModel = value; OnPropertyChanged("SelectedViewModel"); }
        }



        public NavigationVM()
        {
            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);
            //intial startup page
            SelectedViewModel = new DataUsageSummaryVM();
        }

        private void OpenDataUsageSum(object obj)
        {
            SelectedViewModel = new DataUsageSummaryVM();
        }

        private void OpenDataUsageDet(object obj)
        {
            SelectedViewModel = new DataUsageDetailedVM();
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
