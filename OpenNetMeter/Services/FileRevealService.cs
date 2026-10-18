using System;
using System.Diagnostics;
using System.IO;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Services;

public sealed class FileRevealService : IFileRevealService
{
    public void Reveal(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            if (OperatingSystem.IsWindows() && File.Exists(filePath))
            {
                // Opens Explorer with the file selected.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    // Windows paths can't contain quotes, so this quoting is safe.
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = false
                });
                return;
            }

            // Elsewhere there is no portable "select file", so open the containing folder.
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to reveal file '{filePath}'", ex);
        }
    }
}
