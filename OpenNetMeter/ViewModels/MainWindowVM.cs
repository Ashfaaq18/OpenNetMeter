using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using OpenNetMeter.Models;
using System.Threading.Tasks;
using System.Threading;

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
            History,
            Settings
        }

        public MainWindowVM(MiniWidgetVM mwvm_DataContext, ConfirmationDialogVM cD_DataContext) //runs once during app init
        {
            DownloadSpeed = 0;
            UploadSpeed = 0;

            networkStatus = "";

            mwvm = mwvm_DataContext;
            dusvm = new DataUsageSummaryVM();
            duhvm = new DataUsageHistoryVM();
            dudvm = new DataUsageDetailedVM();
            svm = new SettingsVM();

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
            SwitchTabCommand = new BaseCommand(SwitchTab);
        }

        private void UpdateData()
        {
            // -------------- Set download speed across all vms -------------- //
            DownloadSpeed = netProc.DownloadSpeed;
            UploadSpeed = netProc.UploadSpeed;

            mwvm.DownloadSpeed = DownloadSpeed;
            mwvm.UploadSpeed = UploadSpeed;

            dusvm.CurrentSessionDownloadData = netProc.CurrentSessionDownloadData;
            dusvm.CurrentSessionUploadData = netProc.CurrentSessionUploadData;

            dusvm.Graph.DrawPoints(DownloadSpeed, UploadSpeed);

            // -------------- Update current session details -------------- //
            if (netProc.MyProcesses != null && dudvm.MyProcesses != null)
            {
                netProc.IsBufferTime = true;
                //Debug.WriteLine("Buffer var: Start");
                using (ApplicationDB dB = new ApplicationDB(netProc.AdapterName))
                {
                    if (dB.CreateTable() < 0)
                        Debug.WriteLine("Error: Create table");
                    else
                    {
                        foreach (KeyValuePair<string, MyProcess?> app in netProc.MyProcesses) //the contents of this loops remain only for a sec (related to NetworkProcess.cs=>CaptureNetworkSpeed())
                        {
                            dudvm.MyProcesses.TryAdd(app.Key, new MyProcess(app.Key, 0, 0, null));
                            if (app.Value!.CurrentDataRecv == 0 && app.Value!.CurrentDataSend == 0)
                            {
                                Debug.WriteLine($"Both zero {app.Key}");
                            }
                            dudvm.MyProcesses[app.Key].CurrentDataRecv += app.Value!.CurrentDataRecv;
                            dudvm.MyProcesses[app.Key].CurrentDataSend += app.Value!.CurrentDataSend;

                            dB.InsertUniqueRow_ProcessTable(app.Key);

                            long dateID = dB.GetID_DateTable(DateTime.Today);
                            //long dateID = dB.GetID_DateTable(DateTime.Today.AddDays(-3));
                            long processID = dB.GetID_ProcessTable(app.Key);

                            if (dB.InsertUniqueRow_ProcessDateTable(processID, dateID, 
                                (long)dudvm.MyProcesses[app.Key].CurrentDataRecv,
                                (long)dudvm.MyProcesses[app.Key].CurrentDataSend) < 1)
                            {
                                dB.UpdateRow_ProcessDateTable(processID, dateID,
                                    (long)app.Value!.CurrentDataRecv,
                                    (long)app.Value!.CurrentDataSend);
                            }
                        }
                        //Thread.Sleep(800);
                        netProc.MyProcesses.Clear();
                    }
                    netProc.IsBufferTime = false;
                }       
                // Debug.WriteLine("Buffer var: Stop");
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
                    NetworkStatus = netProc.IsNetworkOnline;
                    break;
                default:
                    break;
            }
            sw.Stop();
            Debug.WriteLine($"elapsed time (NetProc): {sw.ElapsedMilliseconds}");
        }

        private void SwitchTab(object obj)
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
            if (netProc != null)
            {
                netProc.PropertyChanged -= NetProc_PropertyChanged;
                netProc.Dispose();
            }
        }
    }
}
