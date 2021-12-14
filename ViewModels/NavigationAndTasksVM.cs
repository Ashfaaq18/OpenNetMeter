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
        NetworkInfo NI;
        DataUsageSummaryVM dusvm;
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

            //intial startup page
            dusvm = new DataUsageSummaryVM();
            SelectedViewModel = dusvm;

            //background thread to trace network info through NetworkInfo
            backgroundWorkers[0] = new BackgroundWorker { WorkerReportsProgress = true };
            backgroundWorkers[0].WorkerReportsProgress = true;
            backgroundWorkers[0].DoWork += RunNetworkInfo;
            backgroundWorkers[0].ProgressChanged += AfterDetectingNewProcess;
            backgroundWorkers[0].RunWorkerAsync();


            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);

            
        }

        private void AfterDetectingNewProcess(object sender, ProgressChangedEventArgs e)
        {
            Debug.WriteLine("PID: " + e.ProgressPercentage.ToString());
        }

        private void RunNetworkInfo(object sender, DoWorkEventArgs e)
        {
            NI = new NetworkInfo(ref dusvm);
        }

        private void OpenDataUsageSum(object obj)
        {
            SelectedViewModel = dusvm;
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
