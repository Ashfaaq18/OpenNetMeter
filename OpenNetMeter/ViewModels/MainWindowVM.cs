using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using OpenNetMeter.Models;
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

        private long initTodayTotalDownloadData = 0;
        private long initTodayTotalUploadData = 0;

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

            //get todays data usage details from the database
            using (ApplicationDB dB = new ApplicationDB(netProc.AdapterName))
            {
                (long, long) todaySum = dB.GetTodayDataSum_ProcessDateTable();
                initTodayTotalDownloadData = todaySum.Item1;
                initTodayTotalUploadData = todaySum.Item2;
            }
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

        private void UpdateMiniWidgetSpeed()
        {
            mwvm.DownloadSpeed = DownloadSpeed;
            mwvm.UploadSpeed = UploadSpeed;
        }

        private void UpdateSummaryTab()
        {
            //summary tab graph points
            dusvm.Graph.DrawPoints(DownloadSpeed, UploadSpeed);

            //summary tab session usage variables
            dusvm.CurrentSessionDownloadData = netProc.CurrentSessionDownloadData;
            dusvm.CurrentSessionUploadData = netProc.CurrentSessionUploadData;

            dusvm.TodayDownloadData = initTodayTotalDownloadData + netProc.CurrentSessionDownloadData;
            dusvm.TodayUploadData = initTodayTotalUploadData + netProc.CurrentSessionUploadData;
        }

        private void UpdateDetailedTab()
        {
            if (netProc.MyProcesses != null && netProc.MyProcessesBuffer != null && dudvm.MyProcesses != null && netProc.PushToDBBuffer != null)
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
                            date1 = date2;
                        }

                        foreach (KeyValuePair<string, MyProcess_Big> app in dudvm.MyProcesses)
                        {
                            dudvm.MyProcesses[app.Key].CurrentDataRecv = 0;
                            dudvm.MyProcesses[app.Key].CurrentDataSend = 0;
                        }

                        netProc.IsBufferTime = true;

                        //this dictionary is locked from being accessible by the other threads like the network data capture Recv()
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

                                /*
                                Debug.WriteLine($"CurrentDataRecv:  {dudvm.MyProcesses[app.Key].CurrentDataRecv} , "    +
                                                $"CurrentDataSend:  {dudvm.MyProcesses[app.Key].CurrentDataSend} , "    +
                                                $"TotalDataRecv:    {dudvm.MyProcesses[app.Key].TotalDataRecv} , "      +
                                                $"TotalDataSend:    {dudvm.MyProcesses[app.Key].TotalDataSend} , "      );
                                */

                                lock (netProc.PushToDBBuffer)
                                {
                                    //push data to a buffer which will be pushed to the DB later
                                    netProc.PushToDBBuffer!.TryAdd(app.Key, new MyProcess_Small(app.Key, 0, 0));
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataRecv += dudvm.MyProcesses[app.Key].CurrentDataRecv;
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataSend += dudvm.MyProcesses[app.Key].CurrentDataSend;
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
                                dudvm.MyProcesses.TryAdd(app.Key, new MyProcess_Big(app.Key, 0, 0, 0, 0));
                                if (app.Value!.CurrentDataRecv == 0 && app.Value!.CurrentDataSend == 0)
                                {
                                    Debug.WriteLine($"Both zero {app.Key}");
                                }
                                dudvm.MyProcesses[app.Key].CurrentDataRecv += app.Value!.CurrentDataRecv;
                                dudvm.MyProcesses[app.Key].CurrentDataSend += app.Value!.CurrentDataSend;
                                dudvm.MyProcesses[app.Key].TotalDataRecv += app.Value!.CurrentDataRecv;
                                dudvm.MyProcesses[app.Key].TotalDataSend += app.Value!.CurrentDataSend;

                                lock (netProc.PushToDBBuffer)
                                {
                                    //push data to a buffer which will be pushed to the DB later
                                    netProc.PushToDBBuffer!.TryAdd(app.Key, new MyProcess_Small(app.Key, 0, 0));
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataRecv = dudvm.MyProcesses[app.Key].TotalDataRecv;
                                    netProc.PushToDBBuffer[app.Key]!.CurrentDataRecv = dudvm.MyProcesses[app.Key].TotalDataSend;
                                }
                            }

                            netProc.MyProcessesBuffer.Clear();
                        }
                    }
                }
            }
        }

        private void UpdateData()
        {
            date2 = DateTime.Now;

            UpdateMainWinSpeed();

            UpdateMiniWidgetSpeed();

            UpdateSummaryTab();

            UpdateDetailedTab();
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
            // Debug.WriteLine($"elapsed time (NetProc): {sw.ElapsedMilliseconds}");
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
