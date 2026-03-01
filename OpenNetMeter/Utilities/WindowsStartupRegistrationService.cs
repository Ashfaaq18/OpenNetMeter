using System;
using System.IO;
using OpenNetMeter.PlatformAbstractions;
using TaskScheduler = Microsoft.Win32.TaskScheduler;

namespace OpenNetMeter.Utilities
{
    public sealed class WindowsStartupRegistrationService : IStartupRegistrationService
    {
        private const string TaskFolder = "OpenNetMeter";
        private const string TaskName = "OpenNetMeterLogon";

        public bool IsEnabled()
        {
            try
            {
                TaskScheduler.TaskFolder folder = TaskScheduler.TaskService.Instance.RootFolder.SubFolders[TaskFolder];
                return folder.Tasks.Exists(TaskName);
            }
            catch
            {
                return false;
            }
        }

        public void SetEnabled(bool enabled, bool startMinimized)
        {
            try
            {
                TaskScheduler.TaskFolder folder = TaskScheduler.TaskService.Instance.RootFolder.SubFolders[TaskFolder];
                if (!enabled)
                {
                    for (int i = 0; i < folder.Tasks.Count; i++)
                    {
                        folder.DeleteTask(folder.Tasks[i].Name);
                    }

                    TaskScheduler.TaskService.Instance.RootFolder.DeleteFolder(TaskFolder);
                    return;
                }
            }
            catch (Exception ex)
            {
                EventLogger.Error("Error while updating startup task registration", ex);
            }

            if (enabled)
            {
                try
                {
                    TaskScheduler.TaskService.Instance.RootFolder.CreateFolder(TaskFolder);
                    CreateTask(startMinimized);
                }
                catch (Exception ex)
                {
                    EventLogger.Error("Error creating startup task folder/definition", ex);
                }
            }
        }

        private static void CreateTask(bool startMinimized)
        {
            try
            {
                TaskScheduler.TaskDefinition td = TaskScheduler.TaskService.Instance.NewTask();
                td.RegistrationInfo.Description = "Run OpenNetMeter on system log on";
                td.Principal.RunLevel = TaskScheduler.TaskRunLevel.Highest;
                td.Principal.LogonType = TaskScheduler.TaskLogonType.InteractiveToken;
                td.Settings.DisallowStartIfOnBatteries = false;
                td.Settings.StopIfGoingOnBatteries = false;
                td.Settings.Compatibility = TaskScheduler.TaskCompatibility.V2_3;

                TaskScheduler.LogonTrigger logonTrigger = new TaskScheduler.LogonTrigger
                {
                    Enabled = true,
                    UserId = null
                };
                td.Triggers.Add(logonTrigger);

                TaskScheduler.ExecAction action = new TaskScheduler.ExecAction
                {
                    Path = Path.Combine(AppContext.BaseDirectory, "OpenNetMeter.exe")
                };
                if (startMinimized)
                    action.Arguments = "/StartMinimized";
                td.Actions.Add(action);

                TaskScheduler.TaskService.Instance.RootFolder.SubFolders[TaskFolder].RegisterTaskDefinition(TaskName, td);
            }
            catch (Exception ex)
            {
                EventLogger.Error("Error creating startup task", ex);
            }
        }
    }
}
