using System;
using System.Net;
using System.Threading;
using OpenNetMeter.Models;
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
