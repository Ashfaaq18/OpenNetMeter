﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using OpenNetMeter.Models;
using System.Threading.Tasks;
using System.Linq;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels
{
    public class MainWindowVM : INotifyPropertyChanged, IDisposable
    {
        private readonly DataUsageSummaryVM dusvm;
        private readonly DataUsageDetailedVM dudvm;
        private readonly DataUsageHistoryVM duhvm;
        private readonly MiniWidgetVM mwvm;
        private readonly SettingsVM svm;
        private readonly NetworkProcess netProc;
        public ICommand SwitchTabCommand { get; set; }

        private int tabBtnToggle;
        public int TabBtnToggle
        {
            get { return tabBtnToggle; }
            set { tabBtnToggle = value; OnPropertyChanged("TabBtnToggle"); }
        }

        private object? selectedViewModel;
        public object? SelectedViewModel
        {
            get { return selectedViewModel; }
            set { selectedViewModel = value; OnPropertyChanged("SelectedViewModel"); }
        }
        public long downloadSpeed;
        public long DownloadSpeed
        {
            get { return downloadSpeed; }
            set { downloadSpeed = value; OnPropertyChanged("DownloadSpeed"); }
        }
        public long uploadSpeed;
        public long UploadSpeed
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

        private DateTime date1;
        private DateTime date2;

        private enum TabPage
        {
            Summary,
            Detailed,
            History,
            Settings
        }

        public MainWindowVM(MiniWidgetVM mw_DataContext, ConfirmationDialogVM cd_DataContext) //runs once during app init
        {
            DownloadSpeed = 0;
            UploadSpeed = 0;
            date1 = DateTime.Now;
            date2 = DateTime.Now;

            networkStatus = "";

            mwvm = mw_DataContext;
            svm = new SettingsVM(mw_DataContext, cd_DataContext);
            svm.PropertyChanged += Svm_PropertyChanged;
            dusvm = new DataUsageSummaryVM();
            duhvm = new DataUsageHistoryVM();
            dudvm = new DataUsageDetailedVM();

            netProc = new NetworkProcess();
            netProc.PropertyChanged += NetProc_PropertyChanged;
            netProc.Initialize(); //have to call this after subscribing to property changer

            duhvm.GetAllDBFiles();

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
                case ((int)TabPage.History):
                    SelectedViewModel = duhvm;
                    break;
                case ((int)TabPage.Settings):
                    SelectedViewModel = svm;
                    break;
                default:
                    SelectedViewModel = dusvm;
                    TabBtnToggle = ((int)TabPage.Summary);
                    break;
            }

            //assign basecommand
            SwitchTabCommand = new BaseCommand(SwitchTab, true);
        }

        private void Svm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "DeleteAllFiles":
                    if (svm.DeleteAllFiles)
                    {
                        if (netProc.IsNetworkOnline != "Disconnected")
                        {
                            netProc.EndNetworkProcess();
                            duhvm.DeleteAllDBFiles();
                            netProc.StartNetworkProcess();
                        }
                        else
                        {
                            duhvm.DeleteAllDBFiles();
                        }
                        svm.DeleteAllFiles = false;
                    }
                    break;
                case "NetworkSpeedFormat":
                    dusvm.Graph.ChangeYLabel();
                    dusvm.Graph.DrawClear();
                    break;
                default:
                    break;
            }
        }

        private void UpdateSummaryTab()
        {

        }

        private void UpdateDetailedTab()
        {

        }

        private void UpdateData()
        {
            date2 = DateTime.Now;
            // -------------- Set network speed across all vms -------------- //

            //main window speed variables
            DownloadSpeed = netProc.DownloadSpeed;
            UploadSpeed = netProc.UploadSpeed;

            //mini widget speed variables
            mwvm.DownloadSpeed = DownloadSpeed;
            mwvm.UploadSpeed = UploadSpeed;

            //summary tab graph points
            dusvm.Graph.DrawPoints(DownloadSpeed, UploadSpeed);

            //summary tab session usage variables
            dusvm.CurrentSessionDownloadData = netProc.CurrentSessionDownloadData;
            dusvm.CurrentSessionUploadData = netProc.CurrentSessionUploadData;

            // -------------- Update current session details -------------- //
            if (netProc.MyProcesses != null && netProc.MyProcessesBuffer != null && dudvm.MyProcesses != null)
            {
                //Debug.WriteLine("Buffer var: Start");
                using (ApplicationDB dB = new ApplicationDB(netProc.AdapterName))
                {
                    if (dB.CreateTable() < 0)
                        Debug.WriteLine("Error: Create table");
                    else
                    {
                        if ((date2.Date - date1.Date).Days > 0)
                        {
                            dusvm.TodayDownloadData = 0;
                            dusvm.TodayUploadData = 0;
                            dB.UpdateDatesInDB();
                            duhvm.UpdateDates();
                            date1 = date2;
                        }

                        foreach (KeyValuePair<string, MyProcess_Big> app in dudvm.MyProcesses)
                        {
                            dudvm.MyProcesses[app.Key].CurrentDataRecv = 0;
                            dudvm.MyProcesses[app.Key].CurrentDataSend = 0;
                        }

                        netProc.IsBufferTime = true;
                        //while the below loop is running, the data packet info will be recorded
                        //to the buffer dictionary (netProc.MyProcessesBuffer)

                        lock (netProc.MyProcesses)
                        {
                            foreach (KeyValuePair<string, MyProcess_Small?> app in netProc.MyProcesses) //the contents of this loops remain only for a sec (related to NetworkProcess.cs=>CaptureNetworkSpeed())
                            {
                                dudvm.MyProcesses.TryAdd(app.Key, new MyProcess_Big(app.Key, 0, 0, 0, 0));
                                if (app.Value!.CurrentDataRecv == 0 && app.Value!.CurrentDataSend == 0)
                                {
                                    Debug.WriteLine($"Both zero {app.Key}");
                                }
                                dudvm.MyProcesses[app.Key].CurrentDataRecv = app.Value!.CurrentDataRecv;
                                dudvm.MyProcesses[app.Key].CurrentDataSend = app.Value!.CurrentDataSend;
                                dudvm.MyProcesses[app.Key].TotalDataRecv += app.Value!.CurrentDataRecv;
                                dudvm.MyProcesses[app.Key].TotalDataSend += app.Value!.CurrentDataSend;

                                dB.InsertUniqueRow_ProcessTable(app.Key);

                                long dateID = dB.GetID_DateTable(DateTime.Today);
                                long processID = dB.GetID_ProcessTable(app.Key);

                                if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID,
                                    dudvm.MyProcesses[app.Key].TotalDataRecv,
                                    dudvm.MyProcesses[app.Key].TotalDataSend) < 1)
                                {
                                    dB.UpdateRow_ProcessDateTable(processID, dateID,
                                        app.Value!.CurrentDataRecv,
                                        app.Value!.CurrentDataSend);
                                }
                            }

                            netProc.MyProcesses.Clear();
                        }
                        
                        netProc.IsBufferTime = false;
                        //the data saved to the buffer dictionary is now extracted here.
                        //The data packet info will now be recorded back into the normal dictionary (netProc.MyProcesses)
                        lock(netProc.MyProcessesBuffer)
                        {
                            foreach (KeyValuePair<string, MyProcess_Small?> app in netProc.MyProcessesBuffer) //the contents of this loops remain only for a sec (related to NetworkProcess.cs=>CaptureNetworkSpeed())
                            {
                                Debug.WriteLine("BUFFEEERRRRR!!!!!");
                                dudvm.MyProcesses.TryAdd(app.Key, new MyProcess_Big(app.Key, 0, 0, 0, 0));
                                if (app.Value!.CurrentDataRecv == 0 && app.Value!.CurrentDataSend == 0)
                                {
                                    Debug.WriteLine($"Both zero {app.Key}");
                                }
                                dudvm.MyProcesses[app.Key].CurrentDataRecv += app.Value!.CurrentDataRecv;
                                dudvm.MyProcesses[app.Key].CurrentDataSend += app.Value!.CurrentDataSend;
                                dudvm.MyProcesses[app.Key].TotalDataRecv += app.Value!.CurrentDataRecv;
                                dudvm.MyProcesses[app.Key].TotalDataSend += app.Value!.CurrentDataSend;

                                dB.InsertUniqueRow_ProcessTable(app.Key);

                                long dateID = dB.GetID_DateTable(DateTime.Today);
                                long processID = dB.GetID_ProcessTable(app.Key);

                                if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID,
                                    dudvm.MyProcesses[app.Key].TotalDataRecv,
                                    dudvm.MyProcesses[app.Key].TotalDataSend) < 1)
                                {
                                    dB.UpdateRow_ProcessDateTable(processID, dateID,
                                        app.Value!.CurrentDataRecv,
                                        app.Value!.CurrentDataSend);
                                }
                            }

                            netProc.MyProcessesBuffer.Clear();
                        }

                        (long, long) todaySum = dB.GetTodayDataSum_ProcessDateTable();
                        dusvm.TodayDownloadData = todaySum.Item1;
                        dusvm.TodayUploadData = todaySum.Item2;
                    }
                }
            }
        }

        private void NetProc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            switch (e.PropertyName)
            {
                case "DownloadSpeed":
                    UpdateData();
                    break;
                case "IsNetworkOnline":
                    if(netProc.IsNetworkOnline == "Disconnected")
                    {
                        NetworkStatus = "Disconnected";
                        if(dudvm.MyProcesses.Count() > 0)
                        {
                            foreach (var row in dudvm.MyProcesses.ToList())
                            {
                                dudvm.MyProcesses.Remove(row.Key);
                            }
                        }
                        dusvm.Graph.DrawClear();
                        dusvm.TodayDownloadData = 0;
                        dusvm.TodayUploadData = 0;
                    }
                    else
                    {
                        NetworkStatus = "Connected : " + netProc.IsNetworkOnline;
                    }
                    break;
                default:
                    break;
            }
            sw.Stop();
            Debug.WriteLine($"elapsed time (NetProc): {sw.ElapsedMilliseconds}");
        }

        private void SwitchTab(object? obj)
        {
            string? tab = obj as string;
            switch (tab)
            {
                case "summary":
                    if (TabBtnToggle != ((int)TabPage.Summary))
                    {
                        SelectedViewModel = dusvm;
                        TabBtnToggle = ((int)TabPage.Summary);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                case "detailed":
                    if (TabBtnToggle != ((int)TabPage.Detailed))
                    {
                        SelectedViewModel = dudvm;
                        TabBtnToggle = ((int)TabPage.Detailed);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                case "history":
                    if (TabBtnToggle != ((int)TabPage.History))
                    {
                        SelectedViewModel = duhvm;
                        TabBtnToggle = ((int)TabPage.History);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                case "settings":
                    if (TabBtnToggle != ((int)TabPage.Settings))
                    {
                        SelectedViewModel = svm;
                        TabBtnToggle = ((int)TabPage.Settings);
                        Properties.Settings.Default.LaunchPage = TabBtnToggle;
                        Properties.Settings.Default.Save();
                    }
                    break;
                default:
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public void Dispose()
        {
            duhvm.Dispose();
            if (netProc != null)
            {
                netProc.PropertyChanged -= NetProc_PropertyChanged;
                netProc.Dispose();
            }
        }
    }
}
