using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
                    Properties.Settings.Default.StartWithWin = value;
                    Properties.Settings.Default.Save();

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

        private bool setDarkMode;
        public bool SetDarkMode
        {
            get { return setDarkMode; }

            set
            {
                if (setDarkMode != value)
                {
                    setDarkMode = value;
                    OnPropertyChanged("SetDarkMode");

                    //set the app settings
                    Properties.Settings.Default.DarkMode = value;
                    Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.MinimizeOnStart = value;
                    Properties.Settings.Default.Save();
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
                    Properties.Settings.Default.NetworkType = value;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private bool miniWidgetTransparent;

        public bool MiniWidgetTransparent
        {
            get { return miniWidgetTransparent; }
            set
            {
                miniWidgetTransparent = value;
                OnPropertyChanged("MiniWidgetTransparent");

                Properties.Settings.Default.MiniWidgetTransparent = value;
                Properties.Settings.Default.Save();
            }
        }



        public SettingsVM()
        {
            taskFolder = "OpenNetMeter";
            taskName = "OpenNetMeter" + "-" + Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString(3);

            //start with windows setting
            UnlockOptionStartWin = true;
            SetStartWithWin = Properties.Settings.Default.StartWithWin;
            MinimizeOnStart = Properties.Settings.Default.MinimizeOnStart;
            MiniWidgetTransparent = Properties.Settings.Default.MiniWidgetTransparent;

            if (SetStartWithWin)
                UnlockMinimizeOnStart = false;
            else
                UnlockMinimizeOnStart = true;

            //network traffic setting
            NetworkTrafficType = Properties.Settings.Default.NetworkType;

            //DarkMode setting
            SetDarkMode = Properties.Settings.Default.DarkMode;
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
                if(MinimizeOnStart)
                    action.Arguments = "/StartMinimized";
                td.Actions.Add(action);

                // Register the task in the sub folder
                TaskScheduler.TaskService.Instance.RootFolder.SubFolders["OpenNetMeter"].RegisterTaskDefinition(taskName, td);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Error: " + ex.Message);
            }
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }   
    }
}
