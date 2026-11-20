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

// Changes SettingsManager.Current.NetworkType, which AdapterSwitchTests and others also change: run serially with them.
[Collection(ApplicationDbCollection.Name)]
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

        service.TrafficBatchObserved += (_, _) =>
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

        Assert.True(eventFired, "TrafficBatchObserved was not raised");
        Assert.True(locksReleasedDuringEvent, "Buffer locks were still held while TrafficBatchObserved was raised");
    }

    [Fact]
    public void EmitProcessTraffic_EmitsBufferedTraffic()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        long observedDownload = 0;
        service.TrafficBatchObserved += (_, e) =>
        {
            foreach (var traffic in e.Traffic)
                observedDownload += traffic.Received;
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
    public void EmitProcessTraffic_StagesEachTickForTheDatabaseAndTheTicksAddUp()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        bool pendingLockHeldDuringEvent = false;
        service.TrafficBatchObserved += (_, _) => pendingLockHeldDuringEvent |= Monitor.IsEntered(netProc.PushToDBBuffer);

        var remote = IPAddress.Parse("93.184.216.34");
        var local = IPAddress.Parse("192.168.1.50");
        netProc.TestInvokeRecvProcess(remote, local, 400, "chrome");
        netProc.TestInvokeSendProcess(local, remote, 50, "chrome");
        netProc.TestInvokeRecvProcess(remote, local, 200, "teams");
        netProc.DownloadSpeed = 1;

        lock (netProc.PushToDBBuffer)
        {
            Assert.Equal(2, netProc.PushToDBBuffer.Count);
            Assert.Equal((400L, 50L), (netProc.PushToDBBuffer["chrome"]!.CurrentDataRecv, netProc.PushToDBBuffer["chrome"]!.CurrentDataSend));
        }

        netProc.TestInvokeRecvProcess(remote, local, 100, "chrome");
        netProc.TestInvokeSendProcess(local, remote, 7, "code");
        netProc.DownloadSpeed = 2;
        // A tick with no traffic stages nothing new.
        netProc.DownloadSpeed = 3;

        lock (netProc.PushToDBBuffer)
        {
            Assert.Equal(3, netProc.PushToDBBuffer.Count);
            Assert.Equal((500L, 50L), (netProc.PushToDBBuffer["chrome"]!.CurrentDataRecv, netProc.PushToDBBuffer["chrome"]!.CurrentDataSend));
            Assert.Equal((200L, 0L), (netProc.PushToDBBuffer["teams"]!.CurrentDataRecv, netProc.PushToDBBuffer["teams"]!.CurrentDataSend));
            Assert.Equal((0L, 7L), (netProc.PushToDBBuffer["code"]!.CurrentDataRecv, netProc.PushToDBBuffer["code"]!.CurrentDataSend));
        }

        Assert.False(pendingLockHeldDuringEvent, "The pending-write buffer was locked while TrafficBatchObserved was raised");
    }

    [Fact]
    public void EmitProcessTraffic_RaisesOneBatchPerTickCarryingEveryProcess()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        var batches = new List<IReadOnlyList<ProcessTraffic>>();
        service.TrafficBatchObserved += (_, e) => batches.Add(e.Traffic);

        var remote = IPAddress.Parse("93.184.216.34");
        var local = IPAddress.Parse("192.168.1.50");
        netProc.TestInvokeRecvProcess(remote, local, 400, "chrome");
        netProc.TestInvokeSendProcess(local, remote, 50, "chrome");
        netProc.TestInvokeRecvProcess(remote, local, 200, "teams");
        netProc.TestInvokeSendProcess(local, remote, 7, "code");
        netProc.DownloadSpeed = 1;

        // Every process and both directions in a single event, not one event per process and direction.
        var first = Assert.Single(batches);
        Assert.Equal(3, first.Count);
        Assert.Contains(new ProcessTraffic("chrome", 400, 50), first);
        Assert.Contains(new ProcessTraffic("teams", 200, 0), first);
        Assert.Contains(new ProcessTraffic("code", 0, 7), first);

        // An idle tick still raises its batch (empty): subscribers use it as their clock.
        netProc.DownloadSpeed = 2;
        Assert.Equal(2, batches.Count);
        Assert.Empty(batches[1]);

        netProc.TestInvokeRecvProcess(remote, local, 100, "chrome");
        netProc.DownloadSpeed = 3;
        Assert.Equal(3, batches.Count);
        Assert.Equal(new ProcessTraffic("chrome", 100, 0), Assert.Single(batches[2]));
    }

    [Fact]
    public void EmitProcessTraffic_SumsNamesThatDifferOnlyByCaseAndClearsTheBuffers()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = CreateNetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);

        var batches = new List<IReadOnlyList<ProcessTraffic>>();
        service.TrafficBatchObserved += (_, e) => batches.Add(e.Traffic);

        var remote = IPAddress.Parse("93.184.216.34");
        var local = IPAddress.Parse("192.168.1.50");

        // Both spellings in the same buffer, and a third in the other buffer.
        netProc.TestInvokeRecvProcess(remote, local, 100, "Chrome");
        netProc.TestInvokeRecvProcess(remote, local, 50, "chrome");
        netProc.TestInvokeSendProcess(local, remote, 5, "chrome");
        netProc.IsBufferTime = true;
        netProc.TestInvokeRecvProcess(remote, local, 25, "CHROME");

        netProc.DownloadSpeed = 1;

        // Case-insensitively one process; the bytes are added, nothing is thrown or dropped.
        var batch = Assert.Single(batches);
        var chrome = Assert.Single(batch);
        Assert.Equal("chrome", chrome.Name, ignoreCase: true);
        Assert.Equal((175L, 5L), (chrome.Received, chrome.Sent));

        // What the tick took is gone from both buffers, so the next tick doesn't count it again.
        Assert.Empty(netProc.MyProcesses);
        Assert.Empty(netProc.MyProcessesBuffer);
        netProc.DownloadSpeed = 2;
        Assert.Empty(batches[1]);

        // The database buffer got the summed row as well.
        lock (netProc.PushToDBBuffer)
        {
            var staged = Assert.Single(netProc.PushToDBBuffer);
            Assert.Equal((175L, 5L), (staged.Value!.CurrentDataRecv, staged.Value.CurrentDataSend));
        }
    }

    [Fact]
    public void BuildBatch_SumsDuplicatesAcrossBuffersAndSkipsEmptyEntries()
    {
        var main = new Dictionary<string, MyProcess_Small?>
        {
            ["Chrome"] = new MyProcess_Small("Chrome", 100, 10),
            ["idle"] = new MyProcess_Small("idle", 0, 0),
            ["gone"] = null,
        };
        var buffer = new Dictionary<string, MyProcess_Small?>
        {
            ["chrome"] = new MyProcess_Small("chrome", 1, 2),
            ["teams"] = new MyProcess_Small("teams", 0, 3),
        };

        var batch = WindowsNetworkCaptureService.BuildBatch(main, buffer);

        Assert.Equal(2, batch.Count);
        var chrome = Assert.Single(batch, t => string.Equals(t.Name, "chrome", StringComparison.OrdinalIgnoreCase));
        Assert.Equal((101L, 12L), (chrome.Received, chrome.Sent));
        Assert.Contains(new ProcessTraffic("teams", 0, 3), batch);
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
