using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Avalonia.Media.Imaging;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace OpenNetMeter.Avalonia.Services;

public sealed class WindowsProcessIconService : IProcessIconService
{
    private readonly ConcurrentDictionary<string, AvaloniaBitmap?> cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly AvaloniaBitmap? DefaultIcon = CreateDefaultIcon();

    public object? GetProcessIcon(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return DefaultIcon;

        return cache.GetOrAdd(processName, LoadIcon);
    }

    private static AvaloniaBitmap? LoadIcon(string processName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(processName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
            return DefaultIcon;

        try
        {
            var processes = Process.GetProcessesByName(nameWithoutExtension);
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                            continue;

                        using Icon? icon = Icon.ExtractAssociatedIcon(path);
                        if (icon == null)
                            continue;

                        using var bitmap = icon.ToBitmap();
                        using var ms = new MemoryStream();
                        bitmap.Save(ms, ImageFormat.Png);
                        ms.Position = 0;
                        return new AvaloniaBitmap(ms);
                    }
                    catch (Exception ex)
                    {
                        EventLogger.Error($"Failed to fetch icon for process '{processName}' from candidate instance", ex);
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to resolve process icon for '{processName}'", ex);
        }

        return DefaultIcon;
    }

    private static AvaloniaBitmap? CreateDefaultIcon()
    {
        try
        {
            using var icon = (Icon)SystemIcons.Application.Clone();
            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return new AvaloniaBitmap(ms);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to create default process icon", ex);
            return null;
        }
    }
}
