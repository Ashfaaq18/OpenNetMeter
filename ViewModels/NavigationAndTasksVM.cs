using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    class NavigationAndTasksVM : INotifyPropertyChanged
    {
        private readonly DataUsageSummaryVM dusvm;
        private readonly DataUsageDetailedVM dudvm;
        private readonly TrayPopupVM tpvm;
        private readonly SettingsVM svm;
        private readonly NetworkInfo netInfo;
        public ICommand DataUsageSumCommand { get; set; }
        public ICommand DataUsageDetCommand { get; set; }
        public ICommand DataUsageSetCommand { get; set; }

        private int tabBtnToggle;
        public int TabBtnToggle
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
        public ulong downloadSpeed;
        public ulong DownloadSpeed
        {
            get { return downloadSpeed; }
            set
            {
                downloadSpeed = value;
                OnPropertyChanged("DownloadSpeed");
            }
        }
        public ulong uploadSpeed;
        public ulong UploadSpeed
        {
            get { return uploadSpeed; }
            set
            {
                uploadSpeed = value;
                OnPropertyChanged("UploadSpeed");
            }
        }

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

        public NavigationAndTasksVM(TrayPopupVM tpVM_DataContext) //runs once during app init
        {
            if (!NetworkInfo.IsAdminMode())
            {
                MessageBox.Show("Please run me as an Administrator");
                Environment.Exit(0);
            }

            DownloadSpeed = 0;
            UploadSpeed = 0;

            //initialize pages
            tpvm = tpVM_DataContext;
            dusvm = new DataUsageSummaryVM(ref tpvm);
            dudvm = new DataUsageDetailedVM(ref dusvm);
            svm = new SettingsVM();

            netInfo = new NetworkInfo(ref dusvm, ref dudvm);
            dudvm.SetNetInfo(ref netInfo);

            //intial startup page
            SelectedViewModel = dusvm;
            TabBtnToggle = 0;

            netInfo.PropertyChanged += NetInfo_PropertyChanged;
            netInfo.InitConnection();

            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);
            DataUsageSetCommand = new BaseCommand(OpenDataUsageSet);
        }

        private void NetInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NetworkStatus = netInfo.IsNetworkOnline;
            //update status bar speeds
            DownloadSpeed = netInfo.DownloadSpeed;
            UploadSpeed = netInfo.UploadSpeed;
            //show speed in tray popup
            tpvm.DownloadSpeed = netInfo.DownloadSpeed;
            tpvm.UploadSpeed = netInfo.UploadSpeed;
            //update graph data points
            dusvm.SpeedGraph.DownloadSpeed = DownloadSpeed;
            dusvm.SpeedGraph.UploadSpeed = UploadSpeed;
        }

        private void OpenDataUsageSum(object obj)
        {
            if (TabBtnToggle != 0)
            {
                SelectedViewModel = dusvm;
                TabBtnToggle = 0;
            }
        }

        private void OpenDataUsageDet(object obj)
        {
            if(TabBtnToggle!=1)
            {
                SelectedViewModel = dudvm;
                TabBtnToggle = 1;
            }
        }
        private void OpenDataUsageSet(object obj)
        {
            if (TabBtnToggle != 2)
            {
                SelectedViewModel = svm;
                TabBtnToggle = 2;
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
