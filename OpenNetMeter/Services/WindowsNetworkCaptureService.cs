using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Versioning;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsNetworkCaptureService : INetworkCaptureService
{
    // Volatile so RefreshAdapterSelection can read it without syncLock (see there).
    private volatile NetworkProcess? networkProcess;
    private readonly object syncLock = new();
    private bool disposed;
    // Written before NetworkChanged is raised and read by late subscribers; a reference write is atomic,
    // volatile makes it visible across threads without a lock.
    private volatile NetworkSnapshotChangedEventArgs? currentSnapshot;

    public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
    public event EventHandler<NetworkTrafficBatchEventArgs>? TrafficBatchObserved;

    public NetworkSnapshotChangedEventArgs? CurrentSnapshot => currentSnapshot;

    public WindowsNetworkCaptureService()
    {
    }

    internal WindowsNetworkCaptureService(NetworkProcess networkProcess)
    {
        this.networkProcess = networkProcess;
        this.networkProcess.PropertyChanged += NetworkProcess_PropertyChanged;
        this.networkProcess.TrafficFlushRequested += NetworkProcess_TrafficFlushRequested;
    }

    public void Start()
    {
        lock (syncLock)
        {
            ThrowIfDisposed();
            if (networkProcess != null)
                return;

            networkProcess = new NetworkProcess();
            networkProcess.PropertyChanged += NetworkProcess_PropertyChanged;
            networkProcess.TrafficFlushRequested += NetworkProcess_TrafficFlushRequested;
            networkProcess.Initialize();
        }
    }

    public void Stop()
    {
        lock (syncLock)
        {
            if (networkProcess == null)
                return;

            networkProcess.PropertyChanged -= NetworkProcess_PropertyChanged;
            // Still subscribed while it disposes: stopping hands over the last partial second through this event.
            networkProcess.Dispose();
            networkProcess.TrafficFlushRequested -= NetworkProcess_TrafficFlushRequested;
            networkProcess = null;
        }
    }

    public IReadOnlyList<NetworkAdapterOption> GetAvailableAdapters()
    {
        return NetworkProcess.GetSelectableAdapters();
    }

    public void RefreshAdapterSelection()
    {
        // Deliberately not under syncLock: the background Start() holds it for its whole duration and this is
        // called from the UI thread. If the start hasn't created the process yet there is nothing to
        // re-evaluate, because Start reads the saved selection itself.
        networkProcess?.RequestNetworkReevaluation();
    }

    public void Dispose()
    {
        lock (syncLock)
        {
            if (disposed)
                return;

            Stop();
            disposed = true;
        }
    }

    private void NetworkProcess_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (networkProcess == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(NetworkProcess.IsNetworkOnline):
                var isDisconnected = string.Equals(
                    networkProcess.IsNetworkOnline,
                    "Disconnected",
                    StringComparison.OrdinalIgnoreCase);

                var snapshot = new NetworkSnapshotChangedEventArgs(
                    isDisconnected ? string.Empty : networkProcess.IsNetworkOnline,
                    isDisconnected ? string.Empty : networkProcess.CurrentAdapterId);

                // Store before raising so a subscriber that attaches (and then reads CurrentSnapshot) while this
                // runs sees this snapshot at the latest.
                currentSnapshot = snapshot;
                NetworkChanged?.Invoke(this, snapshot);
                break;
            case nameof(NetworkProcess.DownloadSpeed):
                // The speed tick: always one batch, empty or not, because subscribers clock on it.
                EmitProcessTraffic(raiseWhenEmpty: true);
                break;
        }
    }

    // An adapter switch: hand over what the old adapter counted since the last tick. It isn't a tick, so an empty
    // batch would only add a spurious sample for subscribers that clock on batches.
    private void NetworkProcess_TrafficFlushRequested()
    {
        EmitProcessTraffic(raiseWhenEmpty: false);
    }

    private void EmitProcessTraffic(bool raiseWhenEmpty)
    {
        var process = networkProcess;
        if (process == null)
            return;

        // Build the batch and clear both buffers while their locks are held, flipping the active
        // write buffer, then release the locks before doing any expensive work
        // (event dispatch, DB staging). Keeping the locks only for the copy
        // and clear prevents the ETW capture thread from being blocked by slow
        // UI or DB operations during sustained high-throughput traffic.
        List<ProcessTraffic> batch;

        lock (process.MyProcesses)
        lock (process.MyProcessesBuffer)
        {
            process.IsBufferTime = !process.IsBufferTime;

            try
            {
                batch = BuildBatch(process.MyProcesses, process.MyProcessesBuffer);
            }
            finally
            {
                // Whatever happens above, never leave a tick's traffic in the buffers: it would be counted again.
                process.MyProcesses.Clear();
                process.MyProcessesBuffer.Clear();
            }
        }

        if (batch.Count == 0 && !raiseWhenEmpty)
            return;

        // Staged before the event: the whole tick is merged under one short lock, and a throwing subscriber
        // then can't keep the tick's traffic out of the database.
        StageForDatabase(process, batch);

        TrafficBatchObserved?.Invoke(this, new NetworkTrafficBatchEventArgs(batch));
    }

    /// <summary>
    /// One entry per process that moved data, summed across the buffers. Names that differ only by case are one
    /// process (the UI keys them case-insensitively), so they are added together rather than copied into a
    /// case-insensitive dictionary, which would throw on the duplicate.
    /// </summary>
    internal static List<ProcessTraffic> BuildBatch(params Dictionary<string, MyProcess_Small?>[] sources)
    {
        var merged = new Dictionary<string, (long Received, long Sent)>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            foreach (var (name, usage) in source)
            {
                if (usage == null || (usage.CurrentDataRecv <= 0 && usage.CurrentDataSend <= 0))
                    continue;

                merged.TryGetValue(name, out var total);
                merged[name] = (total.Received + usage.CurrentDataRecv, total.Sent + usage.CurrentDataSend);
            }
        }

        var batch = new List<ProcessTraffic>(merged.Count);
        foreach (var (name, total) in merged)
            batch.Add(new ProcessTraffic(name, total.Received, total.Sent));

        return batch;
    }

    /// <summary>
    /// Adds a tick's per-process deltas to the buffer the next database push writes. One lock acquisition for the
    /// whole tick rather than one per process, so a push copying the buffer out waits for at most one short merge.
    /// </summary>
    private static void StageForDatabase(NetworkProcess process, IReadOnlyList<ProcessTraffic> batch)
    {
        if (batch.Count == 0)
            return;

        lock (process.PushToDBBuffer)
        {
            foreach (var traffic in batch)
            {
                if (!process.PushToDBBuffer.TryGetValue(traffic.Name, out var pending) || pending == null)
                {
                    pending = new MyProcess_Small(traffic.Name, 0, 0);
                    process.PushToDBBuffer[traffic.Name] = pending;
                }

                pending.CurrentDataRecv += traffic.Received;
                pending.CurrentDataSend += traffic.Sent;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(WindowsNetworkCaptureService));
    }
}

