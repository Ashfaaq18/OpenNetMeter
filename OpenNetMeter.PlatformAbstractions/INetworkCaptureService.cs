using System;
using System.Collections.Generic;

namespace OpenNetMeter.PlatformAbstractions;

public interface INetworkCaptureService : IDisposable
{
    event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
    event EventHandler<NetworkTrafficEventArgs>? TrafficObserved;

    void Start();
    void Stop();

    /// <summary>Adapters the user can pin in Settings (Up, with a usable address). May block; call off the UI thread.</summary>
    IReadOnlyList<NetworkAdapterOption> GetAvailableAdapters();

    /// <summary>Re-evaluates which adapter is monitored after the selection setting changed.</summary>
    void RefreshAdapterSelection();
}

public sealed record NetworkAdapterOption(string Id, string DisplayName);

public sealed class NetworkSnapshotChangedEventArgs : EventArgs
{
    public NetworkSnapshotChangedEventArgs(string adapterName, string adapterId)
    {
        AdapterName = adapterName;
        AdapterId = adapterId;
    }

    public string AdapterName { get; }
    public string AdapterId { get; }
}

public sealed class NetworkTrafficEventArgs : EventArgs
{
    public NetworkTrafficEventArgs(string processName, long bytes, bool isReceive)
    {
        ProcessName = processName;
        Bytes = bytes;
        IsReceive = isReceive;
    }

    public string ProcessName { get; }
    public long Bytes { get; }
    public bool IsReceive { get; }
}
