using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
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
                if (networkSpeedFormat != value)
                {
                    networkSpeedFormat = value;
                    SettingsManager.Current.NetworkSpeedFormat = value;
                    SettingsManager.Save();
                    OnPropertyChanged("NetworkSpeedFormat");
                }
            }
        }
        private string? dataFolder;
        public string DataFolder
        {
            get { return dataFolder ?? " "; }
            set
            {
                if (dataFolder != value)
                {
                    var oldvalue = dataFolder;
                    dataFolder = value;
                    SettingsManager.Current.Folder = value;
                    SettingsManager.Save();
                    OnPropertyChanged("DataFolder");
                    Debug.WriteLine($"folder:{dataFolder}");
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
                    if (value)
                        OnPropertyChanged("DeleteAllFiles");
                }
            }
        }
        public ICommand ResetBtn { get; set; }

        private ConfirmationDialogVM? cdvm;
        private MiniWidgetVM? mwvm;
        public ICommand? BrowseFolderCommand { get; }

        public SettingsVM(MiniWidgetVM mw_ref, ConfirmationDialogVM cdvm_ref)
        {

            mwvm = mw_ref;
            cdvm = cdvm_ref;
            cdvm.BtnCommand = new BaseCommand(ResetDataYesOrNo, true);
            cdvm.DialogMessage = "Warning!!! This will delete all saved profiles.\nDo you still want to continue?";
            taskFolder = "OpenNetMeter";
            taskName = "OpenNetMeter" + "-" + Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString(3);

            //start with windows setting
            UnlockOptionStartWin = true;
            SetStartWithWin = SettingsManager.Current.StartWithWin;
            MinimizeOnStart = SettingsManager.Current.MinimizeOnStart;
            DarkMode = SettingsManager.Current.DarkMode;
            MiniWidgetTransparentSlider = SettingsManager.Current.MiniWidgetTransparentSlider;
            DataFolder = SettingsManager.Current.Folder;

            BrowseFolderCommand = new BaseCommand(BrowseFolder, true);


            if (SetStartWithWin)
                UnlockMinimizeOnStart = false;
            else
                UnlockMinimizeOnStart = true;

            NetworkTrafficType = SettingsManager.Current.NetworkType;

            NetworkSpeedFormat = SettingsManager.Current.NetworkSpeedFormat;

            ResetBtn = new BaseCommand(ResetData, true);

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
            if (cdvm != null)
                cdvm.IsVisible = System.Windows.Visibility.Visible;
        }
        private void BrowseFolder(object? obj)
        {
            var oldvalue = DataFolder;
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    DataFolder = dialog.SelectedPath;
                }
            }
            moveAndDelete(oldvalue, dataFolder);

        }
        private void moveAndDelete(string? oldFolderPath, string? newPath)
        {
            if (oldFolderPath == null || !Directory.Exists(oldFolderPath))
                oldFolderPath = AppDomain.CurrentDomain.BaseDirectory;
            if (newPath == null || !Directory.Exists(newPath))
                newPath = AppDomain.CurrentDomain.BaseDirectory;

            string[] files = Directory.GetFiles(oldFolderPath);

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string destFile = Path.Combine(newPath, fileName);

                try
                {
                    // Move the file
                    File.Move(filePath, destFile);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Error moving file {fileName}: {ex.Message}");
                }
            }
        }

        private void ResetDataYesOrNo(object? obj)
        {
            if (obj != null)
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
                    for (int i = 0; i < sub.Tasks.Count; i++)
                    {
                        sub.DeleteTask(sub.Tasks[i].Name);
                    }

                    TaskScheduler.TaskService.Instance.RootFolder.DeleteFolder(taskFolder);
                }
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
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
                        Debug.WriteLine("Error: " + ex1.Message);
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
                if (MinimizeOnStart)
                    action.Arguments = "/StartMinimized";
                td.Actions.Add(action);

                // Register the task in the sub folder
                TaskScheduler.TaskService.Instance.RootFolder.SubFolders["OpenNetMeter"].RegisterTaskDefinition(taskName, td);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error: " + ex.Message);
            }
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
