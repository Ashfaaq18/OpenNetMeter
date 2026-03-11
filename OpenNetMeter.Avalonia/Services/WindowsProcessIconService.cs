using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Avalonia.Media.Imaging;
using OpenNetMeter.PlatformAbstractions;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace OpenNetMeter.Avalonia.Services;

public sealed class WindowsProcessIconService : IProcessIconService
{
    private readonly ConcurrentDictionary<string, AvaloniaBitmap?> cache = new(StringComparer.OrdinalIgnoreCase);

    public object? GetProcessIcon(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return null;

        return cache.GetOrAdd(processName, LoadIcon);
    }

    private static AvaloniaBitmap? LoadIcon(string processName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(processName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
            return null;

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
                    catch
                    {
                        // Ignore per-process access errors and continue.
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }
        catch
        {
            // Ignore top-level errors and return no icon.
        }

        return null;
    }
}
