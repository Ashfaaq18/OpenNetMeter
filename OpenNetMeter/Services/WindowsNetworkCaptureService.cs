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
    private NetworkProcess? networkProcess;
    private readonly object syncLock = new();
    private bool disposed;

    public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
    public event EventHandler<NetworkTrafficEventArgs>? TrafficObserved;

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

                NetworkChanged?.Invoke(
                    this,
                    new NetworkSnapshotChangedEventArgs(
                        isDisconnected ? string.Empty : networkProcess.IsNetworkOnline,
                        isDisconnected ? string.Empty : networkProcess.CurrentAdapterId));
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

            StageForDatabase(app.Key, app.Value.CurrentDataRecv, app.Value.CurrentDataSend);
        }
    }

    private void StageForDatabase(string processName, long receivedBytes, long sentBytes)
    {
        if (networkProcess == null)
            return;

        if (receivedBytes <= 0 && sentBytes <= 0)
            return;

        lock (networkProcess.PushToDBBuffer)
        {
            if (!networkProcess.PushToDBBuffer.TryGetValue(processName, out var pending) || pending == null)
            {
                pending = new MyProcess_Small(processName, 0, 0);
                networkProcess.PushToDBBuffer[processName] = pending;
            }

            pending.CurrentDataRecv += receivedBytes;
            pending.CurrentDataSend += sentBytes;
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(WindowsNetworkCaptureService));
    }
}

