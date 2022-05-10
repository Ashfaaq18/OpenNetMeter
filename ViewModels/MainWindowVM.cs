﻿using System;
using System.ComponentModel;
using System.Windows.Input;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels
{
    public class MainWindowVM : INotifyPropertyChanged
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
            set { downloadSpeed = value; OnPropertyChanged("DownloadSpeed"); }
        }
        public ulong uploadSpeed;
        public ulong UploadSpeed
        {
            get { return uploadSpeed; }
            set { uploadSpeed = value; OnPropertyChanged("UploadSpeed"); }
        }

        private string networkStatus;
        public string NetworkStatus
        {
            get { return networkStatus; }
            set { networkStatus = value; OnPropertyChanged("NetworkStatus"); }
        }

        private enum TabPage
        {
            Summary,
            Detailed,
            Settings
        }

        public MainWindowVM(MiniWidgetVM mwvm_DataContext, ConfirmationDialogVM cD_DataContext) //runs once during app init
        {
            DownloadSpeed = 0;
            UploadSpeed = 0;

            //initialize pages, dusvm == 0, dudvm === 1, svm == 2
            dusvm = new DataUsageSummaryVM();
            dudvm = new DataUsageDetailedVM(cD_DataContext);
            svm = new SettingsVM();

            netProc = new NetworkProcess(dusvm, dudvm, this, mwvm_DataContext);
            dudvm.SetNetProc(netProc);

            //intial startup page

            TabBtnToggle = Properties.Settings.Default.LaunchPage;
            switch (TabBtnToggle)
            {
                case ((int)TabPage.Summary):
                    SelectedViewModel = dusvm;
                    break;
                case ((int)TabPage.Detailed):
                    SelectedViewModel = dudvm;
                    break;
                case ((int)TabPage.Settings):
                    SelectedViewModel = svm;
                    break;
                default:
                    SelectedViewModel = dusvm;
                    TabBtnToggle = ((int)TabPage.Summary);
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
            if (TabBtnToggle != ((int)TabPage.Summary))
            {
                SelectedViewModel = dusvm;
                TabBtnToggle = ((int)TabPage.Summary);
                Properties.Settings.Default.LaunchPage = TabBtnToggle;
                Properties.Settings.Default.Save();
            }
        }

        private void OpenDataUsageDet(object obj)
        {
            if(TabBtnToggle != ((int)TabPage.Detailed))
            {
                SelectedViewModel = dudvm;
                TabBtnToggle = ((int)TabPage.Detailed);
                Properties.Settings.Default.LaunchPage = TabBtnToggle;
                Properties.Settings.Default.Save();
            }
        }
        private void OpenDataUsageSet(object obj)
        {
            if (TabBtnToggle != ((int)TabPage.Settings))
            {
                SelectedViewModel = svm;
                TabBtnToggle = ((int)TabPage.Settings);
                Properties.Settings.Default.LaunchPage = TabBtnToggle;
                Properties.Settings.Default.Save();
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
