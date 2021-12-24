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
        public HashSet<string> NetworkProfiles;
        private readonly DataUsageSummaryVM dusvm;
        private readonly DataUsageDetailedVM dudvm;
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
            //store network profile names here to function as a lookup table
            NetworkProfiles = new HashSet<string>();
            
            //initialize both pages
            dusvm = new DataUsageSummaryVM();
            dudvm = new DataUsageDetailedVM();

            //Read file
           /* try
            {
                using (var stream = new FileStream("Data", FileMode.Open, FileAccess.Read))
                {
                    FileIO.ReadFile_AppInfo(stream);
                }
            }
            catch (Exception e) { Debug.WriteLine("Error: " + e.Message); }*/
            

            //intial startup page
            SelectedViewModel = dusvm;
            TabBtnToggle = true;

            //Network event runner
            NetworkInfo netInfo = new NetworkInfo(ref dusvm, ref dudvm);
            netInfo.PropertyChanged += NetInfo_PropertyChanged;
            netInfo.GetNetworkStatus();

            //assign basecommand
            DataUsageSumCommand = new BaseCommand(OpenDataUsageSum);
            DataUsageDetCommand = new BaseCommand(OpenDataUsageDet);
          
        }

        private void NetInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NetworkInfo n = sender as NetworkInfo;
            NetworkStatus = n.IsNetworkOnline;
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
