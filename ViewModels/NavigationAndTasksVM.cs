using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Windows.Input;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    class NavigationAndTasksVM : INotifyPropertyChanged
    {
        private NetworkInfo NI;
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;

        private BackgroundWorker[] backgroundWorkers;
        public ICommand DataUsageSumCommand { get; set; }
        public ICommand DataUsageDetCommand { get; set; }

        private object selectedViewModel;

        public object SelectedViewModel
        {
            get { return selectedViewModel; }

            set { selectedViewModel = value; OnPropertyChanged("SelectedViewModel"); }
        }

        public NavigationAndTasksVM() //runs once during app init
        {
            //initialize background workers
            backgroundWorkers = new BackgroundWorker[2];

            //initialize both pages
            dusvm = new DataUsageSummaryVM();
            dudvm = new DataUsageDetailedVM();

            //intial startup page
            SelectedViewModel = dusvm;

            //background thread to trace network info through NetworkInfo
            backgroundWorkers[0] = new BackgroundWorker { WorkerReportsProgress = true };
            backgroundWorkers[0].WorkerReportsProgress = true;
            backgroundWorkers[0].DoWork += RunNetworkInfo;
            backgroundWorkers[0].RunWorkerAsync();

            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);
          
        }

        private void RunNetworkInfo(object sender, DoWorkEventArgs e)
        {
            NI = new NetworkInfo(ref dusvm, ref dudvm);
        }

        private void OpenDataUsageSum(object obj)
        {
            SelectedViewModel = dusvm;
        }

        private void OpenDataUsageDet(object obj)
        {
            SelectedViewModel = dudvm;
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
