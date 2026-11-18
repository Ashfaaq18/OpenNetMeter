using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace OpenNetMeter.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessIconService : IProcessIconService
{
    private static readonly Lazy<AvaloniaBitmap?> DefaultIcon = new(CreateDefaultIcon);

    // Loaded icons (including the default-icon fallback), by process name.
    private readonly ConcurrentDictionary<string, object?> cache = new(StringComparer.OrdinalIgnoreCase);
    // Loads that are queued or running, so concurrent requests for one name share a single load.
    private readonly Dictionary<string, Task<object?>> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object pendingLock = new();
    // LoadIcon takes a full process snapshot; running several at once would only add load.
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private readonly Func<string, object?> loadIcon;
    private readonly Func<object?> getDefaultIcon;

    public WindowsProcessIconService()
        : this(LoadIcon, () => DefaultIcon.Value)
    {
        // Build the fallback icon off the startup path; the first row to need it would otherwise pay for it.
        Task.Run(() => DefaultIcon.Value);
    }

    // Test seam: lets tests count loads and their concurrency without touching real processes.
    internal WindowsProcessIconService(Func<string, object?> loadIcon, Func<object?> getDefaultIcon)
    {
        this.loadIcon = loadIcon;
        this.getDefaultIcon = getDefaultIcon;
    }

    public object? GetCachedIcon(string processName)
    {
        if (!string.IsNullOrWhiteSpace(processName) && cache.TryGetValue(processName, out var icon))
            return icon;

        return getDefaultIcon();
    }

    public Task<object?> LoadIconAsync(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return Task.FromResult(getDefaultIcon());

        if (cache.TryGetValue(processName, out var cached))
            return Task.FromResult(cached);

        lock (pendingLock)
        {
            // Re-check under the lock: a load may have finished since the lock-free lookup above.
            if (cache.TryGetValue(processName, out cached))
                return Task.FromResult(cached);

            if (!pending.TryGetValue(processName, out var load))
            {
                load = Task.Run(() => LoadAndCacheAsync(processName));
                pending[processName] = load;
            }

            return load;
        }
    }

    private async Task<object?> LoadAndCacheAsync(string processName)
    {
        object? icon;
        await loadGate.WaitAsync().ConfigureAwait(false);
        try
        {
            icon = loadIcon(processName);
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to load process icon for '{processName}'", ex);
            icon = getDefaultIcon();
        }
        finally
        {
            loadGate.Release();
        }

        lock (pendingLock)
        {
            cache[processName] = icon;
            pending.Remove(processName);
        }

        return icon;
    }

    private static AvaloniaBitmap? LoadIcon(string processName)
    {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(processName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
            return DefaultIcon.Value;

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
                    catch (Win32Exception ex) when (ex.NativeErrorCode == 5 || ex.NativeErrorCode == unchecked((int)0x80004005))
                    {
                        // Access denied on protected processes (csrss, lsass, etc.) is expected
                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                        // Process exited between enumeration and access - race condition
                        continue;
                    }
                    catch (Exception ex)
                    {
                        EventLogger.Error($"Failed to fetch icon for process '{processName}'", ex);
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

        return DefaultIcon.Value;
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

