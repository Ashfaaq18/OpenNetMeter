using System;

namespace OpenNetMeter.PlatformAbstractions;

public interface INetworkCaptureService : IDisposable
{
    event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
    event EventHandler<NetworkTrafficEventArgs>? TrafficObserved;

    void Start();
    void Stop();
}

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
