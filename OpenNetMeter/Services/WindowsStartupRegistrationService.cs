using System;
using System.IO;
using System.Runtime.Versioning;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;
using TaskScheduler = Microsoft.Win32.TaskScheduler;

namespace OpenNetMeter.Services;

[SupportedOSPlatform("windows")]
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

    private void RemoveONMStartupTask()
    {
        try
        {
            TaskScheduler.TaskFolder rootFolder = TaskScheduler.TaskService.Instance.RootFolder;
            if (rootFolder.SubFolders.Exists(TaskFolder))
            {
                TaskScheduler.TaskFolder folder = rootFolder.SubFolders[TaskFolder];
                for (int i = 0; i < folder.Tasks.Count; i++)
                {
                    folder.DeleteTask(folder.Tasks[i].Name);
                }
                rootFolder.DeleteFolder(TaskFolder);
            }
        }
        catch (Exception ex)
        {
            EventLogger.Error("Error while removing startup task registration", ex);
        }
    }

    public void SetEnabled(bool enabled, bool startMinimized)
    {
        if (enabled)
        {
            CreateONMStartupTask(startMinimized);
        }
        else
        {
            RemoveONMStartupTask();
        }
    }

    private static void CreateONMStartupTask(bool startMinimized)
    {
        try
        {
            TaskScheduler.TaskFolder rootFolder = TaskScheduler.TaskService.Instance.RootFolder;
            TaskScheduler.TaskFolder folder;
            if (rootFolder.SubFolders.Exists(TaskFolder))
            {
                folder = rootFolder.SubFolders[TaskFolder];
                for (int i = 0; i < folder.Tasks.Count; i++)
                {
                    folder.DeleteTask(folder.Tasks[i].Name);
                }
            }
            else
            {
                folder = rootFolder.CreateFolder(TaskFolder);
            }

            TaskScheduler.TaskDefinition td = TaskScheduler.TaskService.Instance.NewTask();
            td.RegistrationInfo.Description = "Run OpenNetMeter Avalonia on system log on";
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

            (string path, string? arguments) = ResolveLaunchCommand(startMinimized);

            TaskScheduler.ExecAction action = new TaskScheduler.ExecAction
            {
                Path = path,
                Arguments = arguments ?? string.Empty
            };

            td.Actions.Add(action);

            folder.RegisterTaskDefinition(
                TaskName,
                td,
                TaskScheduler.TaskCreation.CreateOrUpdate,
                null,
                null,
                td.Principal.LogonType);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Error creating startup task", ex);
        }
    }

    private static (string path, string? arguments) ResolveLaunchCommand(bool startMinimized)
    {
        string? processPath = Environment.ProcessPath;
        string minimizedArgument = startMinimized ? " /StartMinimized" : string.Empty;

        if (!string.IsNullOrWhiteSpace(processPath) &&
            !string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Path.GetFileName(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return (processPath, startMinimized ? "/StartMinimized" : null);
        }

        string baseDirectory = AppContext.BaseDirectory;
        string assemblyFileName = $"{AppDomain.CurrentDomain.FriendlyName}.dll";
        string managedEntryPoint = Path.Combine(baseDirectory, assemblyFileName);

        if (File.Exists(managedEntryPoint))
        {
            return ("dotnet", $"\"{managedEntryPoint}\"{minimizedArgument}");
        }

        string fallbackExe = Path.Combine(AppContext.BaseDirectory, "OpenNetMeter.exe");
        return (fallbackExe, startMinimized ? "/StartMinimized" : null);
    }
}

