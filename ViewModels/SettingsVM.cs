using OpenNetMeter.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TaskScheduler = Microsoft.Win32.TaskScheduler;


namespace OpenNetMeter.ViewModels
{
    public class SettingsVM : INotifyPropertyChanged, IDisposable
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

        private bool setDeskBand;
        public bool SetDeskBand
        {
            get { return setDeskBand; }

            set
            {
                if (setDeskBand != value)
                {
                    setDeskBand = value;
                    OnPropertyChanged("SetDeskBand");

                    //set the app settings
                    Properties.Settings.Default.DeskBandSetting = value;
                    Properties.Settings.Default.Save();

                    if (value)
                    {
                        DllRegisterServer();
                        ShowDeskband();
                    }
                    else
                    {
                        HideDeskband();
                        DllUnregisterServer();
                    }

                }
            }
        }

        private bool unlockDeskBand;
        public bool UnlockDeskBand
        {
            get { return unlockDeskBand; }

            set
            {
                if (unlockDeskBand != value)
                {
                    unlockDeskBand = value;
                    OnPropertyChanged("UnlockDeskBand");
                }
            }
        }

        public ulong DownloadSpeed { get; set; }
        public ulong UploadSpeed { get; set; }
        public SettingsVM()
        {
            taskFolder = "OpenNetMeter";
            taskName = "OpenNetMeter" + "-" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            UnlockOptionStartWin = true;
            UnlockDeskBand = true;
            SetStartWithWin = Properties.Settings.Default.StartWithWin;
            SetDeskBand = Properties.Settings.Default.DeskBandSetting;

            DownloadSpeed = 0;
            UploadSpeed = 0;
        }

        // ---------- DeskBand Stuff ---------------------//

        [DllImport("ONM_DeskBand.dll", EntryPoint = "DllRegisterServer")]
        static extern IntPtr DllRegisterServer();

        [DllImport("ONM_DeskBand.dll", EntryPoint = "DllUnregisterServer")]
        static extern IntPtr DllUnregisterServer();

        [DllImport("ONM_DeskBand.dll", EntryPoint = "ShowDeskband")]
        static extern bool ShowDeskband();

        [DllImport("ONM_DeskBand.dll", EntryPoint = "HideDeskband")]
        static extern bool HideDeskband();

        [DllImport("ONM_DeskBand.dll", EntryPoint = "SetDataVars")]
        public static extern void SetDataVars(double down, Int32 downSuffix, double up, Int32 upSuffix);

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
                td.Actions.Add(action);

                // Register the task in the sub folder
                TaskScheduler.TaskService.Instance.RootFolder.SubFolders["OpenNetMeter"].RegisterTaskDefinition(taskName, td);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Error: " + ex.Message);
            }
        }

        public void Dispose()
        {
            if(SetDeskBand)
            {
                HideDeskband();
                DllUnregisterServer();
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
