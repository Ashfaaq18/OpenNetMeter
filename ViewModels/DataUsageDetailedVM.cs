using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using WhereIsMyData.Models;
using WhereIsMyData.ViewModels.DataUsageDetailedPagesVM;

namespace WhereIsMyData.ViewModels
{
    public class DataUsageDetailedVM : INotifyPropertyChanged
    {
        private object selectedViewModel;
        public object SelectedViewModel
        {
            get { return selectedViewModel; }

            set {
                if (selectedViewModel != value)
                {
                    selectedViewModel = value;
                    OnPropertyChanged("SelectedViewModel");
                }
            }
        }

        public string currentConnection;
        public string CurrentConnection
        {
            get { return currentConnection; }

            set
            {
                if (currentConnection != value)
                {
                    currentConnection = value;
                    OnPropertyChanged("CurrentConnection");
                }
            }
        }

        private string date;
        public string Date
        {
            get { return date; }
            set
            {
                date = value; OnPropertyChanged("Date");
            }
        }

        public ObservableCollection<string> Profiles { get; set; }

        private string selectedProfile;
        public string SelectedProfile
        {
            get { return selectedProfile; }
            set {
                selectedProfile = value;
                OnPropertyChanged("SelectedProfile");
            }
        }
        private Process[] process;

        public void GetAppDataInfo(string name, int dataRecv, int dataSend)
        {
            //var watch = Stopwatch.StartNew();
            if (name == null || name == "")
                name = "System";
            if (OnProfVM.MyApps.TryAdd(name, null))
            {
                process = Process.GetProcessesByName(name);
                Icon ic = null;
                
                if (process.Length > 0)
                {
                    try { ic = Icon.ExtractAssociatedIcon(process[0].MainModule.FileName); }
                    catch { Debug.WriteLine("couldnt retrieve icon"); ic = null; }
                }
                OnProfVM.MyApps[name] = new MyAppInfo(name, (ulong)dataRecv, (ulong)dataSend, ic);
            }
            else
            {
                OnProfVM.MyApps[name].TotalDataRecv += (ulong)dataRecv;
                OnProfVM.MyApps[name].TotalDataSend += (ulong)dataSend;
            }

            OnProfVM.MyApps[name].CurrentDataRecv += (ulong)dataRecv;
            OnProfVM.MyApps[name].CurrentDataSend += (ulong)dataSend;
            // watch.Stop();
            //Debug.WriteLine(watch.ElapsedTicks);
            /*implement a task runner in the future to run dictionary addition in the background*/
        }

        public ICommand ResetBtn { get; set; }
        public OfflineProfileVM OffProfVM { get; set; }
        public OnlineProfileVM OnProfVM { get; set; }

        private DataUsageSummaryVM dusvm;
        private NetworkInfo netInfo;
        public DataUsageDetailedVM(ref DataUsageSummaryVM dusvm_ref, ref NetworkInfo netInfo_ref)
        {
            //set references
            dusvm = dusvm_ref;
            netInfo = netInfo_ref;

            //initialize vars
            SelectedProfile = "";
            Profiles = new ObservableCollection<string>();

            //initialize user controls
            OffProfVM = new OfflineProfileVM();
            OnProfVM = new OnlineProfileVM();

            //set default user control page
            SelectedViewModel = OnProfVM;
            PropertyChanged += SelProfileChange;

            //set button command
            ResetBtn = new BaseCommand(ResetTotalData);
        }

        private void ResetTotalData(object obj)
        {
            /*if(SelectedProfile == CurrentConnection)
            {
                dusvm.CurrentSessionDownloadData = 0;
                dusvm.CurrentSessionUploadData = 0;
                dusvm.TotalDownloadData = 0;
                dusvm.TotalUploadData = 0;
                foreach (var row in OnProfVM.MyApps.ToList())
                {
                    OnProfVM.MyApps.Remove(row.Key);
                }

                netInfo.ResetWriteFileAndSpeed();
            }
            else
            {
                foreach (var row in OffProfVM.MyApps.ToList())
                {
                    OffProfVM.MyApps.Remove(row.Key);
                }
            }*/
            
        }

        private void SelProfileChange(object sender, PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case "SelectedProfile":
                    DataUsageDetailedVM temp = sender as DataUsageDetailedVM;
                    string selProf = temp.SelectedProfile;
                    if (selProf != null && selProf != "")
                    {
                        Debug.WriteLine(selProf);
                        SetVM(selProf);
                    }
                    break;
                default:
                    break;
            }

        }

        private void SetVM(string selProf)
        {
            if (selProf != CurrentConnection)
            {
                if (SelectedViewModel != OffProfVM)
                    SelectedViewModel = OffProfVM;
                //read file into the OnProfVM.MyApps1 dictionary
                try
                {
                    string pathString = Path.Combine("Profiles", selProf + ".WIMD");
                    using (FileStream stream = new FileStream(pathString, FileMode.Open, FileAccess.Read))
                    {
                        foreach (var row in OffProfVM.MyApps.ToList())
                        {
                            OffProfVM.MyApps.Remove(row.Key);
                        }
                        FileIO.ReadFile_AppInfo(OffProfVM.MyApps, stream);
                        DateTime dateTime = File.GetCreationTime(pathString);
                        Date = dateTime.ToShortDateString() + " , " + dateTime.ToShortTimeString();
                        int i = 0;
                    }
                }
                catch (Exception e1)
                {
                    Debug.WriteLine("Cant Read: " + e1.Message);
                }

            }
            else
            {
                if (SelectedViewModel != OnProfVM)
                {
                    SelectedViewModel = OnProfVM;
                    string pathString = Path.Combine("Profiles", selProf + ".WIMD");
                    DateTime dateTime = File.GetCreationTime(pathString);
                    Date = dateTime.ToShortDateString() + " , " + dateTime.ToShortTimeString();
                }
            }
        }

        //------property changers---------------//

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
