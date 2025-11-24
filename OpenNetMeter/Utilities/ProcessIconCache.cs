using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace OpenNetMeter.Utilities
{
    public static class ProcessIconCache
    {
        private static readonly ConcurrentDictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ImageSource? DefaultIcon = CreateDefaultIcon();

        public static ImageSource? GetIcon(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return DefaultIcon;

            return IconCache.GetOrAdd(processName, FetchIcon);
        }

        private static ImageSource? FetchIcon(string processName)
        {
            try
            {
                string nameWithoutExtension = Path.GetFileNameWithoutExtension(processName);
                Process[] processes = Process.GetProcessesByName(nameWithoutExtension);
                try
                {
                    Process? process = processes.FirstOrDefault();
                    string? path = process?.MainModule?.FileName;

                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        using Icon? icon = Icon.ExtractAssociatedIcon(path);
                        if (icon != null)
                        {
                            ImageSource image = IconToImgSource.ToImageSource(icon);
                            if (image.CanFreeze)
                                image.Freeze();
                            return image;
                        }
                    }
                }
                finally
                {
                    foreach (Process proc in processes)
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Win32Exception winError)
            {
                EventLogger.Error(winError.Message);
            }
            catch (SystemException sysExError)
            {
                EventLogger.Error(sysExError.Message);
            }

            return DefaultIcon;
        }

        private static ImageSource? CreateDefaultIcon()
        {
            try
            {
                using Icon icon = (Icon)SystemIcons.Application.Clone();
                ImageSource image = IconToImgSource.ToImageSource(icon);
                if (image.CanFreeze)
                    image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
