using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TaskScheduler = Microsoft.Win32.TaskScheduler;


namespace OpenNetMeter.ViewModels
{
    public class SettingsVM : INotifyPropertyChanged
    {
        private bool setStartWithWin;
        public bool SetStartWithWin
        {
            get { return setStartWithWin; }

            set
            {
                if (setStartWithWin != value)
                {
                    setStartWithWin = value;
                    OnPropertyChanged("SetStartWithWin");

                    //set the app settings
                    SettingsManager.Current.StartWithWin = value;
                    SettingsManager.Save();

                    UnlockOptionStartWin = false;
                    //register to task scheduler
                    SetAppAsTask(value);
                    UnlockOptionStartWin = true;

                    if (value)
                        UnlockMinimizeOnStart = false;
                    else
                        UnlockMinimizeOnStart = true;
                }
            }
        }

        private bool unlockOptionStartWin;
        public bool UnlockOptionStartWin
        {
            get { return unlockOptionStartWin; }

            set
            {
                if (unlockOptionStartWin != value)
                {
                    unlockOptionStartWin = value;
                    OnPropertyChanged("UnlockOptionStartWin");
                }
            }
        }

        private bool minimizeOnStart;
        public bool MinimizeOnStart 
        {
            get { return minimizeOnStart; }
            set
            {
                if (minimizeOnStart != value)
                {
                    minimizeOnStart = value;
                    SettingsManager.Current.MinimizeOnStart = value;
                    SettingsManager.Save();
                }
            }
        }

        private bool unlockMinimizeOnStart;
        public bool UnlockMinimizeOnStart 
        {
            get { return unlockMinimizeOnStart; }

            set
            {
                if (unlockMinimizeOnStart != value)
                {
                    unlockMinimizeOnStart = value;
                    OnPropertyChanged("UnlockMinimizeOnStart");
                }
            }
        }

        //0 == private, 1 == public, 2 == both
        private int networkTrafficType;
        public int NetworkTrafficType
        {
            get { return networkTrafficType; }

            set
            {
                if (networkTrafficType != value)
                {
                    networkTrafficType = value;
                    OnPropertyChanged("NetworkTrafficType");

                    //set the app settings
                    SettingsManager.Current.NetworkType = value;
                    SettingsManager.Save();
                }
            }
        }

        private int networkSpeedFormat;
        public int NetworkSpeedFormat
        {
            get { return networkSpeedFormat; }
            set
            {
                if(networkSpeedFormat != value)
                {
                    networkSpeedFormat = value;
                    SettingsManager.Current.NetworkSpeedFormat = value;
                    SettingsManager.Save();
                    OnPropertyChanged("NetworkSpeedFormat");
                }
            }
        }

        private int networkSpeedMagnitude;
        public int NetworkSpeedMagnitude
        {
            get { return networkSpeedMagnitude; }
            set
            {
                if (networkSpeedMagnitude != value)
                {
                    networkSpeedMagnitude = value;
                    SettingsManager.Current.NetworkSpeedMagnitude = value;
                    SettingsManager.Save();
                    OnPropertyChanged("NetworkSpeedMagnitude");
                }
            }
        }

        private bool darkMode;
        public bool DarkMode
        {
            get { return darkMode; }

            set
            {
                darkMode = value;
                OnPropertyChanged("DarkMode");

                //trigger the miniwidget's BackgroundColor property.
                SetMiniWidgetBackgroundColor(value, MiniWidgetTransparentSlider);

                //set the app settings
                SettingsManager.Current.DarkMode = value;
                SettingsManager.Save();
            }
        }

        private int miniWidgetTransparentSlider;
        public int MiniWidgetTransparentSlider
        {
            get { return miniWidgetTransparentSlider; }
            set
            {
                miniWidgetTransparentSlider = value;
                
                OnPropertyChanged("MiniWidgetTransparentSlider");
                //Debug.WriteLine($"MiniWidgetTransparentSlider: {value}");
                
                //trigger the miniwidget's BackgroundColor property.
                SetMiniWidgetBackgroundColor(DarkMode, value);

                SettingsManager.Current.MiniWidgetTransparentSlider = value;
                SettingsManager.Save();
            }
        }

        public bool deleteAllFiles;
        public bool DeleteAllFiles
        {
            get { return deleteAllFiles; }
            set
            {
                if (deleteAllFiles != value)
                {
                    deleteAllFiles = value;
                    if(value)
                        OnPropertyChanged("DeleteAllFiles");
                }
            }
        }
        public ICommand ResetBtn { get; set; }
        public ICommand UpdateCheckBtn { get; set; }
        public ICommand DownloadUpdateBtn { get; set; }

        private bool miniWidgetVisibility;
        public bool MiniWidgetVisibility
        {
            get { return miniWidgetVisibility; }
            set
            {
                if (miniWidgetVisibility != value)
                {
                    miniWidgetVisibility = value;
                    OnPropertyChanged("MiniWidgetVisibility");
                    RequestSetMiniWidgetVisibility?.Invoke(value);
                }
            }
        }

        public event Action<bool>? RequestSetMiniWidgetVisibility;

        public void SyncMiniWidgetVisibility(bool isVisible)
        {
            if (miniWidgetVisibility == isVisible)
                return;

            miniWidgetVisibility = isVisible;
            OnPropertyChanged("MiniWidgetVisibility");
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get { return _isUpdateAvailable; }
            set
            {
                _isUpdateAvailable = value;
                OnPropertyChanged("IsUpdateAvailable");
            }
        }

        private string _updateStatusMessage = string.Empty;
        public string UpdateStatusMessage
        {
            get { return _updateStatusMessage; }
            set
            {
                _updateStatusMessage = value;
                OnPropertyChanged("UpdateStatusMessage");
            }
        }

        private bool _isCheckingForUpdates;
        public bool IsCheckingForUpdates
        {
            get { return _isCheckingForUpdates; }
            set
            {
                _isCheckingForUpdates = value;
                OnPropertyChanged("IsCheckingForUpdates");
            }
        }

        private string DownloadUrl;

        private ConfirmationDialogVM? cdvm;
        private MiniWidgetVM? mwvm;

        public SettingsVM(MiniWidgetVM mw_ref, ConfirmationDialogVM cdvm_ref)
        {
            mwvm = mw_ref;
            cdvm = cdvm_ref;
            cdvm.BtnCommand = new BaseCommand(ResetDataYesOrNo, true);
            cdvm.DialogMessage = "Warning!!! This will delete all saved profiles.\nDo you still want to continue?";

            taskFolder = "OpenNetMeter";
            taskName = "OpenNetMeterLogon";

            //start with windows setting
            UnlockOptionStartWin = true;
            SetStartWithWin = SettingsManager.Current.StartWithWin;
            MinimizeOnStart = SettingsManager.Current.MinimizeOnStart;
            DarkMode = SettingsManager.Current.DarkMode;
            MiniWidgetTransparentSlider = SettingsManager.Current.MiniWidgetTransparentSlider;
            MiniWidgetVisibility = SettingsManager.Current.MiniWidgetVisibility;

            if (SetStartWithWin)
                UnlockMinimizeOnStart = false;
            else
                UnlockMinimizeOnStart = true;

            NetworkTrafficType = SettingsManager.Current.NetworkType;

            NetworkSpeedFormat = SettingsManager.Current.NetworkSpeedFormat;
            NetworkSpeedMagnitude = SettingsManager.Current.NetworkSpeedMagnitude;

            ResetBtn = new BaseCommand(ResetData, true);
            UpdateCheckBtn = new BaseCommand(UpdateCheck, true);
            DownloadUpdateBtn = new BaseCommand(DownloadUpdate, true);
            DownloadUrl = string.Empty;
            IsUpdateAvailable = false;
            UpdateStatusMessage = "Click here to check for new updates";
            IsCheckingForUpdates = false;
            DeleteAllFiles = false;
        }

        private void SetMiniWidgetBackgroundColor(bool darkMode, int transparency)
        {
            // 00XXXXXX -> 0%
            // FFXXXXXX -> 100%
            // 00 -> 0                  -> 0%,      fully transparent
            // FF -> (2^8)-1 (256-1)    -> 100%,    fully opaque
            // example, 77XXXXXX -> (64+32+16+4+2+1) = 119
            //          (119/255) * 100% = 46.67% opaqueness

            mwvm!.BackgroundColor = darkMode ? "#" + (((100 - transparency) * 255) / 100).ToString("x2") + "252525" : "#" + (((100 - transparency) * 255) / 100).ToString("x2") + "f1f1f1"; ;
        }

        private void ResetData(object? obj)
        {
            if(cdvm != null)
                cdvm.IsVisible = System.Windows.Visibility.Visible;
        }

        private async void UpdateCheck(object? obj)
        {
            IsCheckingForUpdates = true;
            UpdateStatusMessage = "Checking for updates...";
            string tempMsgStatus = string.Empty; 
            IsUpdateAvailable = false;

            const int minDisplayTimeMs = 2000; // 2 seconds, this is to show a progress bar for the update check, for better ux
            var stopwatch = Stopwatch.StartNew();

            try
            {
                (Version? latestVersion, string? downloadUrl) = await UpdateChecker.CheckForUpdates();
                if (latestVersion != null && downloadUrl != null)
                {
                    Version? currentVersion = Assembly.GetExecutingAssembly()?.GetName()?.Version;
                    Debug.WriteLine($"download url: {downloadUrl}, current version: {currentVersion}, latest version: {latestVersion}");
                    if (currentVersion != null && latestVersion > currentVersion)
                    {
                        DownloadUrl = downloadUrl;
                        tempMsgStatus = $"A new version {latestVersion} is available!";
                        IsUpdateAvailable = true;
                    }
                    else
                    {
                        tempMsgStatus = "You have the latest version.";
                    }
                }
                else
                {
                    tempMsgStatus = "Error checking for updates.";
                }
            }
            catch (Exception ex)
            {
                tempMsgStatus = "Error checking for updates.";
                EventLogger.Error(ex.Message);
            }
            finally
            {
                stopwatch.Stop();

                int elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                int remainingTime = minDisplayTimeMs - elapsedMs;
                if (remainingTime > 0)
                {
                    await Task.Delay(remainingTime);
                }

                IsCheckingForUpdates = false;
                UpdateStatusMessage = tempMsgStatus;
            }
        }

        private void DownloadUpdate(object? obj)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = DownloadUrl,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                EventLogger.Error(ex.Message);
            }
        }

        private void ResetDataYesOrNo(object? obj)
        {
            if(obj != null)
            {
                if ((string)obj == "Yes")
                    DeleteAllFiles = true;
                if (cdvm != null)
                    cdvm.IsVisible = System.Windows.Visibility.Hidden;
            }
        }

        // --------- Task Scheduler stuff ------------------//
        private readonly string taskName;
        private readonly string taskFolder;
        private void SetAppAsTask(bool set)
        {
            try
            {
                TaskScheduler.TaskFolder sub = TaskScheduler.TaskService.Instance.RootFolder.SubFolders["OpenNetMeter"];
                if (!set)
                {
                    for(int i = 0; i < sub.Tasks.Count; i++)
                    {
                        sub.DeleteTask(sub.Tasks[i].Name);
                    }

                    TaskScheduler.TaskService.Instance.RootFolder.DeleteFolder(taskFolder);
                }
                return;
            }
            catch (Exception ex)
            {
                EventLogger.Error(ex.Message);
            }
            finally
            {
                if (set)
                {
                    try
                    {
                        TaskScheduler.TaskService.Instance.RootFolder.CreateFolder(taskFolder);
                        CreateTask();
                    }
                    catch (Exception ex1)
                    {
                        EventLogger.Error("Error: " + ex1.Message);
                    }
                }
            }
        }

        private void CreateTask()
        {
            try
            {
                //create task
                TaskScheduler.TaskDefinition td = TaskScheduler.TaskService.Instance.NewTask();
                td.RegistrationInfo.Description = "Run OpenNetMeter on system log on";
                // Set to run at the highest privilege
                td.Principal.RunLevel = TaskScheduler.TaskRunLevel.Highest;
                // Task only runs when user is logged on
                td.Principal.LogonType = TaskScheduler.TaskLogonType.InteractiveToken;

                // These settings will ensure it runs even if on battery power
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;

                //set compatibility to windows 10
                td.Settings.Compatibility = TaskScheduler.TaskCompatibility.V2_3;

                //set to launch when user logs on
                TaskScheduler.LogonTrigger logonTrigger = new TaskScheduler.LogonTrigger
                {
                    Enabled = true,
                    UserId = null
                };
                td.Triggers.Add(logonTrigger);

                //set action to run application
                TaskScheduler.ExecAction action = new TaskScheduler.ExecAction();
                action.Path = Path.Combine(AppContext.BaseDirectory, "OpenNetMeter.exe");
                if(MinimizeOnStart)
                    action.Arguments = "/StartMinimized";
                td.Actions.Add(action);

                // Register the task in the sub folder
                TaskScheduler.TaskService.Instance.RootFolder.SubFolders["OpenNetMeter"].RegisterTaskDefinition(taskName, td);
            }
            catch(Exception ex)
            {
                EventLogger.Error("Error: " + ex.Message);
            }
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
