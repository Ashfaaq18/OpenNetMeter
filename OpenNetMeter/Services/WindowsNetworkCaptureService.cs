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
    public event EventHandler<NetworkTrafficEventArgs>? TrafficObserved;

    public NetworkSnapshotChangedEventArgs? CurrentSnapshot => currentSnapshot;

    public WindowsNetworkCaptureService()
    {
    }

    internal WindowsNetworkCaptureService(NetworkProcess networkProcess)
    {
        this.networkProcess = networkProcess;
        this.networkProcess.PropertyChanged += NetworkProcess_PropertyChanged;
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
            networkProcess.Dispose();
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
                EmitProcessTraffic();
                break;
        }
    }

    private void EmitProcessTraffic()
    {
        if (networkProcess == null)
            return;

        // Snapshot both buffers while their locks are held, flip the active
        // write buffer, then release the locks before doing any expensive work
        // (event dispatch, DB staging). Keeping the locks only for the copy
        // and clear prevents the ETW capture thread from being blocked by slow
        // UI or DB operations during sustained high-throughput traffic.
        Dictionary<string, MyProcess_Small?> mainSnapshot;
        Dictionary<string, MyProcess_Small?> bufferSnapshot;

        lock (networkProcess.MyProcesses)
        lock (networkProcess.MyProcessesBuffer)
        {
            networkProcess.IsBufferTime = !networkProcess.IsBufferTime;

            mainSnapshot = new Dictionary<string, MyProcess_Small?>(networkProcess.MyProcesses, StringComparer.OrdinalIgnoreCase);
            bufferSnapshot = new Dictionary<string, MyProcess_Small?>(networkProcess.MyProcessesBuffer, StringComparer.OrdinalIgnoreCase);

            networkProcess.MyProcesses.Clear();
            networkProcess.MyProcessesBuffer.Clear();
        }

        // Staged before the events: the whole tick is merged under one short lock, and a throwing subscriber
        // then can't keep the tick's traffic out of the database.
        StageForDatabase(mainSnapshot, bufferSnapshot);

        EmitSnapshot(mainSnapshot);
        EmitSnapshot(bufferSnapshot);
    }

    private void EmitSnapshot(Dictionary<string, MyProcess_Small?> snapshot)
    {
        foreach (var app in snapshot)
        {
            if (app.Value == null)
                continue;

            if (app.Value.CurrentDataRecv > 0)
                TrafficObserved?.Invoke(this, new NetworkTrafficEventArgs(app.Key, app.Value.CurrentDataRecv, isReceive: true));

            if (app.Value.CurrentDataSend > 0)
                TrafficObserved?.Invoke(this, new NetworkTrafficEventArgs(app.Key, app.Value.CurrentDataSend, isReceive: false));
        }
    }

    /// <summary>
    /// Adds a tick's per-process deltas to the buffer the next database push writes. One lock acquisition for the
    /// whole tick rather than one per process, so a push copying the buffer out waits for at most one short merge.
    /// </summary>
    private void StageForDatabase(params Dictionary<string, MyProcess_Small?>[] snapshots)
    {
        var process = networkProcess;
        if (process == null)
            return;

        lock (process.PushToDBBuffer)
        {
            foreach (var snapshot in snapshots)
            {
                foreach (var (processName, usage) in snapshot)
                {
                    if (usage == null || (usage.CurrentDataRecv <= 0 && usage.CurrentDataSend <= 0))
                        continue;

                    if (!process.PushToDBBuffer.TryGetValue(processName, out var pending) || pending == null)
                    {
                        pending = new MyProcess_Small(processName, 0, 0);
                        process.PushToDBBuffer[processName] = pending;
                    }

                    pending.CurrentDataRecv += usage.CurrentDataRecv;
                    pending.CurrentDataSend += usage.CurrentDataSend;
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(WindowsNetworkCaptureService));
    }
}

