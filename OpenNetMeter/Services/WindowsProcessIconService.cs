using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace OpenNetMeter.Services;

/// <summary>
/// Loads process icons in batches. Requests queue up and one worker drains them together: ONE process snapshot per
/// batch maps the requested names to executable paths (taking a snapshot per icon made a screenful of new rows
/// expensive), then the icons are extracted from those paths with limited parallelism.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsProcessIconService : IProcessIconService
{
    private static readonly Lazy<AvaloniaBitmap?> DefaultIcon = new(CreateDefaultIcon);

    /// <summary>How long the worker waits after the first request, so requests made together share one snapshot.</summary>
    private static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(20);

    /// <summary>Icons extracted at the same time within a batch.</summary>
    private const int IconParallelism = 3;

    // Loaded icons (including the default-icon fallback), by process name.
    private readonly ConcurrentDictionary<string, object?> cache = new(StringComparer.OrdinalIgnoreCase);
    // Loads that are queued or running, so concurrent requests for one name share a single load.
    private readonly Dictionary<string, TaskCompletionSource<object?>> pending = new(StringComparer.OrdinalIgnoreCase);
    // Names waiting for the next batch. Guarded by pendingLock, like pending and workerRunning.
    private readonly List<string> queue = [];
    private readonly object pendingLock = new();
    private bool workerRunning;

    private readonly Func<IReadOnlySet<string>, IReadOnlyDictionary<string, string>> snapshotExecutablePaths;
    private readonly Func<string, object?> loadIconFromPath;
    private readonly Func<object?> getDefaultIcon;
    private readonly Func<Task>? waitForBatch;
    private readonly int iconParallelism;
    private readonly Action<string, Exception> logError;

    public WindowsProcessIconService()
        : this(
            SnapshotExecutablePaths,
            LoadIconFromPath,
            () => DefaultIcon.Value,
            () => Task.Delay(BatchWindow),
            IconParallelism,
            (message, ex) => EventLogger.Error(message, ex))
    {
        // Build the fallback icon off the startup path; the first row to need it would otherwise pay for it.
        Task.Run(() => DefaultIcon.Value);
    }

    // Test seam: lets tests count snapshots and extractions, and decide when a batch starts, without touching real processes.
    // snapshotExecutablePaths gets the wanted process names (no extension) and returns name -> executable path for
    // those it found; waitForBatch (null = don't wait) is what the worker awaits before it drains the queue;
    // logError is where failures are reported (tests replace it so they don't write to the Windows Event Log).
    internal WindowsProcessIconService(
        Func<IReadOnlySet<string>, IReadOnlyDictionary<string, string>> snapshotExecutablePaths,
        Func<string, object?> loadIconFromPath,
        Func<object?> getDefaultIcon,
        Func<Task>? waitForBatch,
        int iconParallelism,
        Action<string, Exception> logError)
    {
        this.snapshotExecutablePaths = snapshotExecutablePaths;
        this.loadIconFromPath = loadIconFromPath;
        this.getDefaultIcon = getDefaultIcon;
        this.waitForBatch = waitForBatch;
        this.iconParallelism = Math.Max(1, iconParallelism);
        this.logError = logError;
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
                load = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                pending[processName] = load;
                queue.Add(processName);

                if (!workerRunning)
                {
                    workerRunning = true;
                    _ = Task.Run(RunWorkerAsync);
                }
            }

            return load.Task;
        }
    }

    /// <summary>Drains the queue batch by batch until it is empty; the next request starts a new worker.</summary>
    private async Task RunWorkerAsync()
    {
        try
        {
            if (waitForBatch != null)
                await waitForBatch().ConfigureAwait(false);

            while (true)
            {
                string[] batch;
                lock (pendingLock)
                {
                    if (queue.Count == 0)
                    {
                        workerRunning = false;
                        return;
                    }

                    batch = [.. queue];
                    queue.Clear();
                }

                LoadBatch(batch);
            }
        }
        catch (Exception ex)
        {
            // Nothing in a batch is expected to throw (each step is guarded), but a request must never wait forever.
            logError("Failed to load process icons", ex);
            FailAllPending();
        }
    }

    private void LoadBatch(string[] names)
    {
        try
        {
            // Blank stems (".exe") have no process to look for.
            var stems = names.ToDictionary(name => name, name => Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase);
            var wanted = stems.Values
                .Where(stem => !string.IsNullOrWhiteSpace(stem))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            IReadOnlyDictionary<string, string> paths = new Dictionary<string, string>();
            if (wanted.Count > 0)
            {
                try
                {
                    paths = snapshotExecutablePaths(wanted);
                }
                catch (Exception ex)
                {
                    logError("Failed to resolve process icons", ex);
                }
            }

            Parallel.ForEach(
                names,
                new ParallelOptions { MaxDegreeOfParallelism = iconParallelism },
                name => Complete(name, LoadIconOrDefault(name, stems[name], paths)));
        }
        catch (Exception ex)
        {
            logError("Failed to load a batch of process icons", ex);
        }
        finally
        {
            // Whatever the batch didn't complete (it failed part-way) gets the default icon instead of hanging.
            foreach (var name in names)
                Complete(name, getDefaultIcon());
        }
    }

    private object? LoadIconOrDefault(string name, string stem, IReadOnlyDictionary<string, string> paths)
    {
        if (string.IsNullOrWhiteSpace(stem) || !paths.TryGetValue(stem, out var path))
            return getDefaultIcon();

        try
        {
            return loadIconFromPath(path) ?? getDefaultIcon();
        }
        catch (Exception ex)
        {
            logError($"Failed to load process icon for '{name}'", ex);
            return getDefaultIcon();
        }
    }

    /// <summary>Caches the icon and releases everyone waiting for it. Does nothing for a name that is already done.</summary>
    private void Complete(string name, object? icon)
    {
        TaskCompletionSource<object?>? load;
        lock (pendingLock)
        {
            if (!pending.Remove(name, out load))
                return;

            cache[name] = icon;
        }

        load.TrySetResult(icon);
    }

    private void FailAllPending()
    {
        string[] names;
        lock (pendingLock)
        {
            names = [.. pending.Keys];
            queue.Clear();
            workerRunning = false;
        }

        foreach (var name in names)
            Complete(name, getDefaultIcon());
    }

    /// <summary>
    /// One process snapshot for a whole batch: the executable path of a process for each wanted name (no extension,
    /// case-insensitive). Only the wanted processes' main modules are opened, which is the expensive part.
    /// </summary>
    private static IReadOnlyDictionary<string, string> SnapshotExecutablePaths(IReadOnlySet<string> wantedNames)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to take a process snapshot for icons", ex);
            return paths;
        }

        try
        {
            foreach (var process in processes)
            {
                string? name = null;
                try
                {
                    name = process.ProcessName;
                    if (!wantedNames.Contains(name) || paths.ContainsKey(name))
                        continue;

                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        paths[name] = path;
                        if (paths.Count == wantedNames.Count)
                            break;
                    }
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
                    EventLogger.Error($"Failed to fetch the executable path of process '{name}'", ex);
                }
            }
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
        }

        return paths;
    }

    private static AvaloniaBitmap? LoadIconFromPath(string path)
    {
        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null)
                return DefaultIcon.Value;

            using var bitmap = icon.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return new AvaloniaBitmap(ms);
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to fetch icon from '{path}'", ex);
            return DefaultIcon.Value;
        }
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
