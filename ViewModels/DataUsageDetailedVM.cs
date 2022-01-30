﻿using System;
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
using OpenNetMeter.Models;
using OpenNetMeter.ViewModels.DataUsageDetailedPagesVM;

namespace OpenNetMeter.ViewModels
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
            if (OnProfVM.MyProcesses.TryAdd(name, null))
            {
                process = Process.GetProcessesByName(name);
                Icon ic = null;
                
                if (process.Length > 0)
                {
                    try { ic = Icon.ExtractAssociatedIcon(process[0].MainModule.FileName); }
                    catch { Debug.WriteLine("couldnt retrieve icon"); ic = null; }
                }
                OnProfVM.MyProcesses[name] = new MyProcess(name, (ulong)dataRecv, (ulong)dataSend, ic);
            }
            else
            {
                OnProfVM.MyProcesses[name].TotalDataRecv += (ulong)dataRecv;
                OnProfVM.MyProcesses[name].TotalDataSend += (ulong)dataSend;
            }

            OnProfVM.MyProcesses[name].CurrentDataRecv += (ulong)dataRecv;
            OnProfVM.MyProcesses[name].CurrentDataSend += (ulong)dataSend;
            // watch.Stop();
            //Debug.WriteLine(watch.ElapsedTicks);
            /*implement a task runner in the future to run dictionary addition in the background*/
        }

        public ICommand ResetBtn { get; set; }
        public OfflineProfileVM OffProfVM { get; set; }
        public OnlineProfileVM OnProfVM { get; set; }

        private DataUsageSummaryVM dusvm;
        private NetworkInfo netInfo;
        public DataUsageDetailedVM(ref DataUsageSummaryVM dusvm_ref)
        {
            //set references
            dusvm = dusvm_ref;

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

        public void SetNetInfo(ref NetworkInfo netInfo_ref)
        {
            netInfo = netInfo_ref;
        }
        private void ResetTotalData(object obj)
        {
            if(SelectedProfile == CurrentConnection)
            {
                dusvm.CurrentSessionDownloadData = 0;
                dusvm.CurrentSessionUploadData = 0;
                dusvm.TotalDownloadData = 0;
                dusvm.TotalUploadData = 0;
                foreach (var row in OnProfVM.MyProcesses.ToList())
                {
                    OnProfVM.MyProcesses.Remove(row.Key);
                }

                netInfo.ResetWriteFileAndSpeed();
            }
            else
            {
                foreach (var row in OffProfVM.MyProcesses.ToList())
                {
                    OffProfVM.MyProcesses.Remove(row.Key);
                }
                Debug.WriteLine("Deleted file: " + SelectedProfile);
                FileIO.DeleteFile(Path.Combine("Profiles", SelectedProfile + ".onm")); //delete file
                Profiles.Remove(SelectedProfile); //remove profile from combo box
            }

            if (Profiles.Count > 0)
                SelectedProfile = Profiles[0];
            else
                SelectedProfile = null;
            
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
            string pathString = Path.Combine("Profiles", selProf + ".onm");
            if (selProf != CurrentConnection)
            {
                SelectedViewModel = OffProfVM;
                //read file into the OnProfVM.MyProcesses1 dictionary
                try
                {
                    using (FileStream stream = new FileStream(pathString, FileMode.Open, FileAccess.Read))
                    {
                        foreach (var row in OffProfVM.MyProcesses.ToList())
                        {
                            OffProfVM.MyProcesses.Remove(row.Key);
                        }
                        FileIO.ReadFile_MyProcess(OffProfVM.MyProcesses, stream);
                        DateTime dateTime = File.GetCreationTime(pathString);
                        Date = dateTime.ToShortDateString() + " , " + dateTime.ToShortTimeString();
                    }
                }
                catch (Exception e1)
                {
                    Debug.WriteLine("Cant Read: " + e1.Message);
                }

            }
            else
            {
                SelectedViewModel = OnProfVM;
                DateTime dateTime = File.GetCreationTime(pathString);
                Date = dateTime.ToShortDateString() + " , " + dateTime.ToShortTimeString();
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
