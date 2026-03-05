using System;
using System.ComponentModel;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia.Services;

public sealed class WindowsNetworkCaptureService : INetworkCaptureService
{
    private NetworkProcess? networkProcess;
    private readonly object syncLock = new();
    private bool disposed;

    public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
    public event EventHandler<NetworkTrafficEventArgs>? TrafficObserved;

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

        networkProcess.IsBufferTime = true;
        lock (networkProcess.MyProcesses)
        {
            foreach (var app in networkProcess.MyProcesses)
            {
                if (app.Value == null)
                    continue;

                if (app.Value.CurrentDataRecv > 0)
                    TrafficObserved?.Invoke(this, new NetworkTrafficEventArgs(app.Key, app.Value.CurrentDataRecv, isReceive: true));

                if (app.Value.CurrentDataSend > 0)
                    TrafficObserved?.Invoke(this, new NetworkTrafficEventArgs(app.Key, app.Value.CurrentDataSend, isReceive: false));
            }

            networkProcess.MyProcesses.Clear();
        }

        networkProcess.IsBufferTime = false;
        lock (networkProcess.MyProcessesBuffer)
        {
            foreach (var app in networkProcess.MyProcessesBuffer)
            {
                if (app.Value == null)
                    continue;

                if (app.Value.CurrentDataRecv > 0)
                    TrafficObserved?.Invoke(this, new NetworkTrafficEventArgs(app.Key, app.Value.CurrentDataRecv, isReceive: true));

                if (app.Value.CurrentDataSend > 0)
                    TrafficObserved?.Invoke(this, new NetworkTrafficEventArgs(app.Key, app.Value.CurrentDataSend, isReceive: false));
            }

            networkProcess.MyProcessesBuffer.Clear();
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(WindowsNetworkCaptureService));
    }
}
