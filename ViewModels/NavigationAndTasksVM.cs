﻿using System;
using System.ComponentModel;
using System.Windows.Input;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels
{
    public class NavigationAndTasksVM : INotifyPropertyChanged, IDisposable
    {
        private readonly DataUsageSummaryVM dusvm;
        private readonly DataUsageDetailedVM dudvm;
        private readonly SettingsVM svm;
        private readonly NetworkProcess netProc;
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

        public NavigationAndTasksVM(TrayPopupVM tpVM_DataContext, ConfirmationDialogVM cD_DataContext) //runs once during app init
        {
            DownloadSpeed = 0;
            UploadSpeed = 0;

            //initialize pages, dusvm == 0, dudvm === 1, svm == 2
            dusvm = new DataUsageSummaryVM();
            dudvm = new DataUsageDetailedVM(cD_DataContext);
            svm = new SettingsVM();

            netProc = new NetworkProcess(dusvm, dudvm, this, tpVM_DataContext);
            dudvm.SetNetProc(netProc);

            //intial startup page

            TabBtnToggle = Properties.Settings.Default.LaunchPage;
            switch (TabBtnToggle)
            {
                case 0:
                    SelectedViewModel = dusvm;
                    break;
                case 1:
                    SelectedViewModel = dudvm;
                    break;
                case 2:
                    SelectedViewModel = svm;
                    break;
                default:
                    SelectedViewModel = dusvm;
                    TabBtnToggle = 0;
                    break;
            }

            netProc.InitConnection();

            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);
            DataUsageSetCommand = new BaseCommand(OpenDataUsageSet);

        }

        private void OpenDataUsageSum(object obj)
        {
            if (TabBtnToggle != 0)
            {
                SelectedViewModel = dusvm;
                TabBtnToggle = 0;
                Properties.Settings.Default.LaunchPage = TabBtnToggle;
                Properties.Settings.Default.Save();
            }
        }

        private void OpenDataUsageDet(object obj)
        {
            if(TabBtnToggle!=1)
            {
                SelectedViewModel = dudvm;
                TabBtnToggle = 1;
                Properties.Settings.Default.LaunchPage = TabBtnToggle;
                Properties.Settings.Default.Save();
            }
        }
        private void OpenDataUsageSet(object obj)
        {
            if (TabBtnToggle != 2)
            {
                SelectedViewModel = svm;
                TabBtnToggle = 2;
                Properties.Settings.Default.LaunchPage = TabBtnToggle;
                Properties.Settings.Default.Save();
            }
        }
        public void Dispose()
        {
            svm.Dispose();
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
