using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    class NavigationAndTasksVM : INotifyPropertyChanged
    {
        private readonly DataUsageSummaryVM dusvm;
        private readonly DataUsageDetailedVM dudvm;
        private readonly NetworkInfo netInfo;
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
        public DataUnits DownloadSpeed { get; set; }
        public DataUnits UploadSpeed { get; set; }

        private string networkStatus;
        public string NetworkStatus
        {
            get { return networkStatus; }
            set
            {
                networkStatus = value; 
                OnPropertyChanged("NetworkStatus");
            }
        }

        public NavigationAndTasksVM() //runs once during app init
        {
            if (!NetworkInfo.IsAdminMode())
            {
                MessageBox.Show("Please run me as an Administrator");
                Environment.Exit(0);
            }

            DownloadSpeed = new DataUnits();
            UploadSpeed = new DataUnits();

            //initialize both pages
            dusvm = new DataUsageSummaryVM();
            dudvm = new DataUsageDetailedVM();

            //intial startup page
            SelectedViewModel = dusvm;
            TabBtnToggle = true;

            //Network event runner
            netInfo = new NetworkInfo(ref dusvm, ref dudvm);
            netInfo.PropertyChanged += NetInfo_PropertyChanged;
            netInfo.InitNetworkStatus(); // update status bar with network status
            netInfo.CaptureNetworkPackets(); //start capturing network packet sizes
            netInfo.CaptureNetworkSpeed(); //start monitoring network speed

            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);

        }

        private void NetInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NetworkStatus = netInfo.IsNetworkOnline;
            //update status bar speeds
            DownloadSpeed = netInfo.DownloadSpeed;
            UploadSpeed = netInfo.UploadSpeed;
            //update graph data points
            dusvm.SpeedGraph.DownloadSpeed = DownloadSpeed;
            dusvm.SpeedGraph.UploadSpeed = UploadSpeed;
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
