using System;
using System.Net;
using OpenNetMeter.Models;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Tests;

public class NetworkProcessTests
{
    private static readonly byte[] LocalIPv4 = IPAddress.Parse("192.168.1.50").GetAddressBytes();
    private static readonly byte[] EmptyIPv6 = new byte[16];

    [Fact]
    public void RecvProcess_AccumulatesDownloadPerProcess()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();

        netProc.TestInvokeRecvProcess(IPAddress.Parse("93.184.216.34"), IPAddress.Parse("192.168.1.50"), 400, "chrome");
        netProc.TestInvokeRecvProcess(IPAddress.Parse("93.184.216.34"), IPAddress.Parse("192.168.1.50"), 200, "chrome");

        Assert.Equal(600, netProc.CurrentSessionDownloadData);
        Assert.True(netProc.MyProcesses!.TryGetValue("chrome", out var process));
        Assert.NotNull(process);
        Assert.Equal(600, process!.CurrentDataRecv);
        Assert.Equal(0, process.CurrentDataSend);
    }

    [Fact]
    public void SendProcess_TracksUploadWhileBuffering()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();
        netProc.IsBufferTime = true;

        netProc.TestInvokeSendProcess(IPAddress.Parse("192.168.1.50"), IPAddress.Parse("203.0.113.10"), 1024, "");

        Assert.Equal(1024, netProc.CurrentSessionUploadData);
        Assert.Empty(netProc.MyProcesses!);
        Assert.True(netProc.MyProcessesBuffer!.TryGetValue("System", out var process));
        Assert.NotNull(process);
        Assert.Equal(1024, process!.CurrentDataSend);
        Assert.Equal(0, process.CurrentDataRecv);
    }

    [Fact]
    public void RecvProcess_PublicOnlyDropsPrivatePeer()
    {
        using var networkType = new NetworkTypeScope(1);
        using var netProc = CreateNetworkProcess();

        netProc.TestInvokeRecvProcess(IPAddress.Parse("192.168.0.10"), IPAddress.Parse("192.168.1.50"), 500, "lan-app");

        Assert.Equal(0, netProc.CurrentSessionDownloadData);
        Assert.Empty(netProc.MyProcesses!);
    }

    [Fact]
    public void RecvProcess_PrivateOnlyAcceptsPrivateTraffic()
    {
        using var networkType = new NetworkTypeScope(0);
        using var netProc = CreateNetworkProcess();

        netProc.TestInvokeRecvProcess(IPAddress.Parse("10.0.0.5"), IPAddress.Parse("192.168.1.50"), 300, "lan");
        netProc.TestInvokeRecvProcess(IPAddress.Parse("93.184.216.34"), IPAddress.Parse("192.168.1.50"), 200, "lan");

        Assert.Equal(300, netProc.CurrentSessionDownloadData);
        Assert.True(netProc.MyProcesses!.TryGetValue("lan", out var process));
        Assert.NotNull(process);
        Assert.Equal(300, process!.CurrentDataRecv);
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
