using System;
using System.Collections.Generic;

namespace OpenNetMeter.PlatformAbstractions;

public interface INetworkCaptureService : IDisposable
{
    event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;

    /// <summary>
    /// Raised once per capture tick (about once a second) with every process's traffic since the previous tick. A
    /// tick that moved nothing raises an empty batch, so subscribers can use the event as their one-second clock.
    /// An adapter switch raises one extra batch, only when non-empty, with the old adapter's last partial second.
    /// Raised on a worker thread.
    /// </summary>
    event EventHandler<NetworkTrafficBatchEventArgs>? TrafficBatchObserved;

    /// <summary>
    /// The last snapshot raised through <see cref="NetworkChanged"/>, or null before the first change. Capture
    /// starts before its subscribers exist, so a late subscriber reads this to catch up on what it missed.
    /// </summary>
    NetworkSnapshotChangedEventArgs? CurrentSnapshot { get; }

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

/// <summary>One process's traffic during one capture tick. Names are unique ignoring case within a batch.</summary>
public readonly record struct ProcessTraffic(string Name, long Received, long Sent);

public sealed class NetworkTrafficBatchEventArgs : EventArgs
{
    public NetworkTrafficBatchEventArgs(IReadOnlyList<ProcessTraffic> traffic)
    {
        Traffic = traffic;
    }

    /// <summary>Bytes received and sent per process since the previous batch; only processes that moved data.</summary>
    public IReadOnlyList<ProcessTraffic> Traffic { get; }
}
