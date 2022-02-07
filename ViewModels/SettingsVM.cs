using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32.TaskScheduler;
using OpenNetMeter.Models;

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

                    if (reg != null)
                    {
                        //reg.RegisterComDLL();
                        reg.Test();
                    }
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

        private Registrar reg;
        public SettingsVM()
        {
            taskFolder = "OpenNetMeter";
            taskName = "OpenNetMeter" + "-" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            UnlockOptionStartWin = true;
            UnlockDeskBand = false;
            SetStartWithWin = Properties.Settings.Default.StartWithWin;
            SetDeskBand = Properties.Settings.Default.DeskBandSetting;
            reg = new Registrar("ONM_DeskBand.dll");
            if (reg != null)
            {
                reg.RegisterComDLL();
                reg.Test();
            }
            /*if(SetDeskBand)
            {
                //register
            }*/
        }

        private readonly string taskName;
        private readonly string taskFolder;
        private void SetAppAsTask(bool set)
        {
            try
            {
                TaskFolder sub = TaskService.Instance.RootFolder.SubFolders["OpenNetMeter"];
                if (!set)
                {
                    for(int i = 0; i < sub.Tasks.Count; i++)
                    {
                        sub.DeleteTask(sub.Tasks[i].Name);
                    }

                    TaskService.Instance.RootFolder.DeleteFolder(taskFolder);
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
                        TaskService.Instance.RootFolder.CreateFolder(taskFolder);
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
                TaskDefinition td = TaskService.Instance.NewTask();
                td.RegistrationInfo.Description = "Run OpenNetMeter on system log on";
                // Set to run at the highest privilege
                td.Principal.RunLevel = TaskRunLevel.Highest;
                // Task only runs when user is logged on
                td.Principal.LogonType = TaskLogonType.InteractiveToken;

                // These settings will ensure it runs even if on battery power
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;

                //set compatibility to windows 10
                td.Settings.Compatibility = TaskCompatibility.V2_3;

                //set to launch when user logs on
                LogonTrigger logonTrigger = new LogonTrigger
                {
                    Enabled = true,
                    UserId = null
                };
                td.Triggers.Add(logonTrigger);

                //set action to run application
                ExecAction action = new ExecAction();
                action.Path = Path.Combine(AppContext.BaseDirectory, "OpenNetMeter.exe");
                td.Actions.Add(action);

                // Register the task in the sub folder
                TaskService.Instance.RootFolder.SubFolders["OpenNetMeter"].RegisterTaskDefinition(taskName, td);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Error: " + ex.Message);
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

        public void Dispose()
        {
            if(reg != null)
            {
                reg.UnRegisterComDLL();
                reg.FreeLib();
            }
        }
    }
}
