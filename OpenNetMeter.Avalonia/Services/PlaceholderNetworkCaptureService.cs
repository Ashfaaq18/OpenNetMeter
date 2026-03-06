using System;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia.Services;

public sealed class PlaceholderNetworkCaptureService : INetworkCaptureService
{
    private bool started;

    public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
    public event EventHandler<NetworkTrafficEventArgs>? TrafficObserved
    {
        add { }
        remove { }
    }

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

    public void Dispose()
    {
        Stop();
    }
}
