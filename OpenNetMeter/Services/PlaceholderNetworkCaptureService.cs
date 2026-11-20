using System;
using System.Collections.Generic;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Services;

public sealed class PlaceholderNetworkCaptureService : INetworkCaptureService
{
    private bool started;

    public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
    public event EventHandler<NetworkTrafficBatchEventArgs>? TrafficBatchObserved
    {
        add { }
        remove { }
    }

    public NetworkSnapshotChangedEventArgs? CurrentSnapshot => null;

    public void Start()
    {
        if (started)
            return;

        started = true;
        NetworkChanged?.Invoke(this, new NetworkSnapshotChangedEventArgs(string.Empty, string.Empty));
    }

    public void Stop()
    {
        started = false;
    }

    public IReadOnlyList<NetworkAdapterOption> GetAvailableAdapters() => [];

    public void RefreshAdapterSelection()
    {
    }

    public void Dispose()
    {
        Stop();
    }
}

