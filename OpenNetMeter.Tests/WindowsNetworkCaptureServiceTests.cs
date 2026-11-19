using System;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Properties;
using OpenNetMeter.Services;
using Xunit;

namespace OpenNetMeter.Tests;

public class WindowsNetworkCaptureServiceTests
{
    private static readonly byte[] LocalIPv4 = IPAddress.Parse("192.168.1.50").GetAddressBytes();
    private static readonly byte[] EmptyIPv6 = new byte[16];

    [Fact]
    public void EmitProcessTraffic_ReleasesBufferLocksBeforeEventDispatch()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        bool eventFired = false;
        bool locksReleasedDuringEvent = false;

        service.TrafficObserved += (_, _) =>
        {
            eventFired = true;
            // If EmitProcessTraffic is correctly implemented, both buffer locks
            // must have been released before raising this event. Holding either
            // lock here would block the ETW capture thread and could freeze the
            // system during sustained high-throughput traffic.
            bool mainLocked = Monitor.IsEntered(netProc.MyProcesses);
            bool bufferLocked = Monitor.IsEntered(netProc.MyProcessesBuffer);
            locksReleasedDuringEvent = !mainLocked && !bufferLocked;
        };

        netProc.TestInvokeRecvProcess(
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("192.168.1.50"),
            400,
            "chrome");

        netProc.DownloadSpeed = 1;

        Assert.True(eventFired, "TrafficObserved was not raised");
        Assert.True(locksReleasedDuringEvent, "Buffer locks were still held while TrafficObserved was raised");
    }

    [Fact]
    public void EmitProcessTraffic_EmitsBufferedTraffic()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        long observedDownload = 0;
        service.TrafficObserved += (_, e) =>
        {
            if (e.IsReceive)
                observedDownload += e.Bytes;
        };

        netProc.TestInvokeRecvProcess(
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("192.168.1.50"),
            400,
            "chrome");
        netProc.TestInvokeRecvProcess(
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("192.168.1.50"),
            200,
            "chrome");

        netProc.DownloadSpeed = 1;

        Assert.Equal(600, observedDownload);
    }

    [Fact]
    public async Task RefreshAdapterSelection_DoesNotWaitForTheStartLock()
    {
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);
        var syncLock = typeof(WindowsNetworkCaptureService)
            .GetField("syncLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;

        // Another thread holds the lock the way the background Start() does while it initialises.
        using var locked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holder = new Thread(() =>
        {
            lock (syncLock)
            {
                locked.Set();
                release.Wait();
            }
        });
        holder.Start();
        try
        {
            Assert.True(locked.Wait(TimeSpan.FromSeconds(5)));

            var refresh = Task.Run(service.RefreshAdapterSelection);

            var finished = await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(ReferenceEquals(refresh, finished), "RefreshAdapterSelection blocked on the start lock");
        }
        finally
        {
            release.Set();
            holder.Join();
        }
    }

    [Fact]
    public void RefreshAdapterSelection_BeforeStartDoesNothing()
    {
        using var service = new WindowsNetworkCaptureService();

        service.RefreshAdapterSelection();
    }

    [Fact]
    public void CurrentSnapshot_IsNullBeforeAnyNetworkChange()
    {
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        Assert.Null(service.CurrentSnapshot);
    }

    [Fact]
    public void CurrentSnapshot_HoldsTheLastRaisedSnapshot()
    {
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        netProc.TestSetNetworkOnline("Wi-Fi : Home", "adapter-1");

        Assert.NotNull(service.CurrentSnapshot);
        Assert.Equal("Wi-Fi : Home", service.CurrentSnapshot!.AdapterName);
        Assert.Equal("adapter-1", service.CurrentSnapshot.AdapterId);

        netProc.TestSetNetworkOnline("Disconnected");

        // A disconnect is the latest snapshot too (empty name and id), not a reason to keep the old one.
        Assert.NotNull(service.CurrentSnapshot);
        Assert.Equal(string.Empty, service.CurrentSnapshot!.AdapterName);
        Assert.Equal(string.Empty, service.CurrentSnapshot.AdapterId);
    }

    [Fact]
    public void CurrentSnapshot_IsStoredBeforeNetworkChangedIsRaised()
    {
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        NetworkSnapshotChangedEventArgs? raised = null;
        NetworkSnapshotChangedEventArgs? currentDuringEvent = null;
        service.NetworkChanged += (_, e) =>
        {
            raised = e;
            currentDuringEvent = service.CurrentSnapshot;
        };

        netProc.TestSetNetworkOnline("Ethernet", "adapter-2");

        Assert.NotNull(raised);
        Assert.Same(raised, currentDuringEvent);
    }

    private static NetworkProcess CreateNetworkProcess()
    {
        var proc = new NetworkProcess();
        proc.TestSetLocalIPs(LocalIPv4, EmptyIPv6);
        return proc;
    }

    private sealed class NetworkTypeScope : IDisposable
    {
        private readonly int originalNetworkType;

        public NetworkTypeScope(int networkType)
        {
            originalNetworkType = SettingsManager.Current.NetworkType;
            SettingsManager.Current.NetworkType = networkType;
        }

        public void Dispose()
        {
            SettingsManager.Current.NetworkType = originalNetworkType;
        }
    }
}
