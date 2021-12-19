using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    class NavigationAndTasksVM : INotifyPropertyChanged
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;
        public ICommand DataUsageSumCommand { get; set; }
        public ICommand DataUsageDetCommand { get; set; }

        private bool tabBtnToggle;
        public bool TabBtnToggle 
        { 
            get { return tabBtnToggle; }
            set { tabBtnToggle = value; OnPropertyChanged("TabBtnToggle"); }
        }   

        private object selectedViewModel;

        public object SelectedViewModel
        {
            get { return selectedViewModel; }

            set { selectedViewModel = value; OnPropertyChanged("SelectedViewModel"); }
        }

        public NavigationAndTasksVM() //runs once during app init
        {
            //initialize both pages
            dusvm = new DataUsageSummaryVM();
            dudvm = new DataUsageDetailedVM();

            //intial startup page
            SelectedViewModel = dusvm;
            TabBtnToggle = true;

            new NetworkInfo(ref dusvm, ref dudvm);

            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);
          
        }

        private void OpenDataUsageSum(object obj)
        {
            SelectedViewModel = dusvm;
            TabBtnToggle = true;
        }

        private void OpenDataUsageDet(object obj)
        {
            SelectedViewModel = dudvm;
            TabBtnToggle = false;
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
