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
        private BackgroundWorker[] backgroundWorkers;

        private List<TCP_Table.TCP_Connection> tcp_table;
        private List<UDP_Table.MIB_UDPROW_OWNER_PID> udp_table;
        public ICommand DataUsageSumCommand { get; set; }

        public ICommand DataUsageDetCommand { get; set; }

        private object selectedViewModel;

        public object SelectedViewModel
        {
            get { return selectedViewModel; }

            set { selectedViewModel = value; OnPropertyChanged("SelectedViewModel"); }
        }

        public NavigationAndTasksVM()
        {

            //initialize background workers
            backgroundWorkers = new BackgroundWorker[2];

            //TCP UDP stuff 
            backgroundWorkers[0] = new BackgroundWorker { WorkerReportsProgress = true };
            backgroundWorkers[0].WorkerReportsProgress = true;
            backgroundWorkers[0].DoWork += Get_TCP_UDP_PIDS;
            backgroundWorkers[0].RunWorkerAsync();

            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);
            //intial startup page
            SelectedViewModel = new DataUsageSummaryVM();
        }

        //get the PIDS of TCP and UDP connections every second
        private void Get_TCP_UDP_PIDS(object sender, DoWorkEventArgs e)
        {
            //BackgroundWorker worker = (BackgroundWorker)sender;
            ManagementEventWatcher watcher = new ManagementEventWatcher();
            watcher.Query = new WqlEventQuery("__InstanceCreationEvent",
                                              new TimeSpan(0, 0, 1),
                                              "TargetInstance isa \"Win32_Process\"");

            ManagementBaseObject eM;

            while (true)
            {
                eM = watcher.WaitForNextEvent(); ;
                //Display information from the event
                Debug.WriteLine(
                    "Process {0} has been created, path is: {1}, PID is {2}",
                    ((ManagementBaseObject)eM["TargetInstance"])["Name"],
                    ((ManagementBaseObject)eM["TargetInstance"])["ExecutablePath"],
                    ((ManagementBaseObject)eM["TargetInstance"])["ProcessId"]);
            }
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
