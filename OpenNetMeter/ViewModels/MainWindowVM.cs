using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using OpenNetMeter.Models;
using System.Linq;
using OpenNetMeter.Utilities;
using OpenNetMeter.Properties;

namespace OpenNetMeter.ViewModels
{
    public class MainWindowVM : INotifyPropertyChanged, IDisposable
    {
        private readonly DataUsageSummaryVM dusvm;
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

        private long initSinceDateTotalDownloadData = 0;
        private long initSinceDateTotalUploadData = 0;
        private long sinceDateSessionDownloadBaseline = 0;
        private long sinceDateSessionUploadBaseline = 0;

        private enum TabPage
        {
            Summary,
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

            netProc = new NetworkProcess();
            netProc.PropertyChanged += NetProc_PropertyChanged;
            netProc.Initialize(); //have to call this after subscribing to property changer
            dusvm.PropertyChanged += Dusvm_PropertyChanged;

            // Populate adapters list from the unified DB
            duhvm.GetAllDBFiles();

            //intial startup page
            TabBtnToggle = SettingsManager.Current.LaunchPage;
            switch (TabBtnToggle)
            {
                case ((int)TabPage.Summary):
                    SelectedViewModel = dusvm;
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

            //get initial data usage details from the database
            RefreshSummaryBaseline();
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

        private void UpdateMainWinSpeed()
        {
            DownloadSpeed = netProc.DownloadSpeed;
            UploadSpeed = netProc.UploadSpeed;
        }

        private void UpdateMiniWidgetValues()
        {
            mwvm.DownloadSpeed = DownloadSpeed;
            mwvm.CurrentSessionDownloadData = netProc.CurrentSessionDownloadData;
            mwvm.UploadSpeed = UploadSpeed;
            mwvm.CurrentSessionUploadData = netProc.CurrentSessionUploadData;
        }

        private void RefreshSummaryBaseline()
        {
            using (ApplicationDB dB = new ApplicationDB(netProc.AdapterName))
            {
                (long, long) totals = dB.GetDataSumBetweenDates(dusvm.SinceDate, DateTime.Today);
                initSinceDateTotalDownloadData = totals.Item1;
                initSinceDateTotalUploadData = totals.Item2;
            }

            sinceDateSessionDownloadBaseline = netProc.CurrentSessionDownloadData;
            sinceDateSessionUploadBaseline = netProc.CurrentSessionUploadData;

            UpdateSummaryTab();
        }
        private void UpdateSummaryTab()
        {
            //summary tab graph points
            dusvm.Graph.DrawPoints(DownloadSpeed, UploadSpeed);

            //summary tab session usage variables
            dusvm.CurrentSessionDownloadData = netProc.CurrentSessionDownloadData;
            dusvm.CurrentSessionUploadData = netProc.CurrentSessionUploadData;

            long sessionDownloadDelta = netProc.CurrentSessionDownloadData - sinceDateSessionDownloadBaseline;
            long sessionUploadDelta = netProc.CurrentSessionUploadData - sinceDateSessionUploadBaseline;

            if (sessionDownloadDelta < 0)
                sessionDownloadDelta = 0;
            if (sessionUploadDelta < 0)
                sessionUploadDelta = 0;

            dusvm.TodayDownloadData = initSinceDateTotalDownloadData + sessionDownloadDelta;
            dusvm.TodayUploadData = initSinceDateTotalUploadData + sessionUploadDelta;

            UpdateMyProcessTable();
        }

        private void UpdateMyProcessTable()
        {
            if (netProc.MyProcesses != null && netProc.MyProcessesBuffer != null && dusvm.MyProcesses != null && netProc.PushToDBBuffer != null)
            {
                using (ApplicationDB dB = new ApplicationDB(netProc.AdapterName))
                {
                    if (dB.CreateTable() < 0)
                        Debug.WriteLine("Error: Create table");
                    else
                    {
                        //when the application stays open during a day transition
                        if ((date2.Date - date1.Date).Days > 0)
                        {
                            dusvm.TodayDownloadData = 0;
                            dusvm.TodayUploadData = 0;
                            dB.UpdateDatesInDB();
                            duhvm.UpdateDates();
                            dusvm.RefreshDateBounds();
                            RefreshSummaryBaseline();
                            date1 = date2;
                        }

                        foreach (KeyValuePair<string, MyProcess_Big> app in dusvm.MyProcesses)
                        {
                            dusvm.MyProcesses[app.Key].CurrentDataRecv = 0;
                            dusvm.MyProcesses[app.Key].CurrentDataSend = 0;
                        }

                        netProc.IsBufferTime = true;

                        //this dictionary is locked from being accessible by the other threads like the network data capture Recv()
                        lock (netProc.MyProcesses)
                        {
                            foreach (KeyValuePair<string, MyProcess_Small?> app in netProc.MyProcesses) //the contents of this loops remain only for a sec (related to NetworkProcess.cs=>CaptureNetworkSpeed())
                            {
                                EnsureProcessEntry(app.Key);
                                if (app.Value!.CurrentDataRecv == 0 && app.Value!.CurrentDataSend == 0)
                                {
                                    Debug.WriteLine($"Both zero {app.Key}");
                                }
                                dusvm.MyProcesses[app.Key].CurrentDataRecv = app.Value!.CurrentDataRecv;
                                dusvm.MyProcesses[app.Key].CurrentDataSend = app.Value!.CurrentDataSend;
                                dusvm.MyProcesses[app.Key].TotalDataRecv += app.Value!.CurrentDataRecv;
                                dusvm.MyProcesses[app.Key].TotalDataSend += app.Value!.CurrentDataSend;

                                /*
                                Debug.WriteLine($"CurrentDataRecv:  {dusvm.MyProcesses[app.Key].CurrentDataRecv} , "    +
                                                $"CurrentDataSend:  {dusvm.MyProcesses[app.Key].CurrentDataSend} , "    +
                                                $"TotalDataRecv:    {dusvm.MyProcesses[app.Key].TotalDataRecv} , "      +
                                                $"TotalDataSend:    {dusvm.MyProcesses[app.Key].TotalDataSend} , "      );
                                */

                                lock (netProc.PushToDBBuffer)
                                {
                                    //push data to a buffer which will be pushed to the DB later
                                    netProc.PushToDBBuffer!.TryAdd(app.Key, new MyProcess_Small(app.Key, 0, 0));
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataRecv += dusvm.MyProcesses[app.Key].CurrentDataRecv;
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataSend += dusvm.MyProcesses[app.Key].CurrentDataSend;
                                }
                            }

                            netProc.MyProcesses.Clear();
                        }

                        netProc.IsBufferTime = false;

                        lock (netProc.MyProcessesBuffer)
                        {
                            foreach (KeyValuePair<string, MyProcess_Small?> app in netProc.MyProcessesBuffer) //the contents of this loops remain only for a sec (related to NetworkProcess.cs=>CaptureNetworkSpeed())
                            {
                                Debug.WriteLine("BUFFEEERRRRR!!!!!");
                                EnsureProcessEntry(app.Key);
                                if (app.Value!.CurrentDataRecv == 0 && app.Value!.CurrentDataSend == 0)
                                {
                                    Debug.WriteLine($"Both zero {app.Key}");
                                }
                                dusvm.MyProcesses[app.Key].CurrentDataRecv += app.Value!.CurrentDataRecv;
                                dusvm.MyProcesses[app.Key].CurrentDataSend += app.Value!.CurrentDataSend;
                                dusvm.MyProcesses[app.Key].TotalDataRecv += app.Value!.CurrentDataRecv;
                                dusvm.MyProcesses[app.Key].TotalDataSend += app.Value!.CurrentDataSend;

                                lock (netProc.PushToDBBuffer)
                                {
                                    //push data to a buffer which will be pushed to the DB later
                                    netProc.PushToDBBuffer!.TryAdd(app.Key, new MyProcess_Small(app.Key, 0, 0));
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataRecv = dusvm.MyProcesses[app.Key].TotalDataRecv;
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataRecv = dusvm.MyProcesses[app.Key].TotalDataSend;
                                }
                            }

                            netProc.MyProcessesBuffer.Clear();
                        }
                    }
                }
            }
        }

        private void EnsureProcessEntry(string processName)
        {
            var icon = ProcessIconCache.GetIcon(processName);

            if (!dusvm.MyProcesses.TryAdd(processName, new MyProcess_Big(processName, 0, 0, 0, 0, icon)))
            {
                if (dusvm.MyProcesses[processName].Icon == null)
                {
                    dusvm.MyProcesses[processName].Icon = icon;
                }
            }
        }

        private void UpdateData()
        {
            date2 = DateTime.Now;

            UpdateMainWinSpeed();

            UpdateMiniWidgetValues();

            UpdateSummaryTab();
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
                    if (netProc.IsNetworkOnline == "Disconnected")
                    {
                        NetworkStatus = "Disconnected";
                        if (dusvm.MyProcesses.Count() > 0)
                        {
                            foreach (var row in dusvm.MyProcesses.ToList())
                            {
                                dusvm.MyProcesses.Remove(row.Key);
                            }
                        }
                        dusvm.Graph.DrawClear();
                        dusvm.TodayDownloadData = 0;
                        dusvm.TodayUploadData = 0;
                    }
                    else
                    {
                        NetworkStatus = "Connected : " + netProc.IsNetworkOnline;
                        // Ensure current adapter exists in DB and refresh profiles
                        using (ApplicationDB dB = new ApplicationDB(netProc.AdapterName))
                        {
                            dB.CreateTable();
                            dB.InsertUniqueRow_AdapterTable(netProc.AdapterName);
                        }
                        duhvm.GetAllDBFiles();
                    }
                    break;
                default:
                    break;
            }
            sw.Stop();
            // Debug.WriteLine($"elapsed time (NetProc): {sw.ElapsedMilliseconds}");
        }

        private void Dusvm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DataUsageSummaryVM.SinceDate))
            {
                RefreshSummaryBaseline();
            }
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
                        SettingsManager.Current.LaunchPage = TabBtnToggle;
                        SettingsManager.Save();
                    }
                    break;
                case "history":
                    if (TabBtnToggle != ((int)TabPage.History))
                    {
                        SelectedViewModel = duhvm;
                        TabBtnToggle = ((int)TabPage.History);
                        SettingsManager.Current.LaunchPage = TabBtnToggle;
                        SettingsManager.Save();
                    }
                    break;
                case "settings":
                    if (TabBtnToggle != ((int)TabPage.Settings))
                    {
                        SelectedViewModel = svm;
                        TabBtnToggle = ((int)TabPage.Settings);
                        SettingsManager.Current.LaunchPage = TabBtnToggle;
                        SettingsManager.Save();
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

            dusvm.PropertyChanged -= Dusvm_PropertyChanged;

            if (netProc != null)
            {
                netProc.PropertyChanged -= NetProc_PropertyChanged;
                netProc.Dispose();
            }
        }
    }
}
