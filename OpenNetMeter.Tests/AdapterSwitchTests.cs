using System.Net;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Properties;
using OpenNetMeter.Services;

namespace OpenNetMeter.Tests;

/// <summary>
/// Switching adapters keeps the ETW session and moves the counting, the database attribution and the status over in
/// place; capture starts before the database is ready; a capture that dies is restarted. The ETW session is a fake
/// (a real one needs administrator rights and is machine-wide) and the database is a temporary file, so these show
/// the sequencing and bookkeeping, not what Windows does to a real session across a switch.
/// </summary>
[Collection(ApplicationDbCollection.Name)]
public class AdapterSwitchTests
{
    private static readonly DateTime Day1 = new(2026, 11, 17);
    private static readonly byte[] WifiIp = IPAddress.Parse("192.168.1.50").GetAddressBytes();
    private static readonly byte[] EthernetIp = IPAddress.Parse("10.0.0.7").GetAddressBytes();
    private static readonly byte[] NoIPv6 = new byte[16];
    private static readonly IPAddress Remote = IPAddress.Parse("93.184.216.34");
    private static readonly IPAddress Wifi = new(WifiIp);
    private static readonly IPAddress Ethernet = new(EthernetIp);

    // ---- The kernel session survives a switch ----

    [Fact]
    public async Task Switch_KeepsTheKernelSessionRunningAndNeverStopsIt()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        var captures = new FakeCaptures();
        using var netProc = NewProcess(captures);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);

        netProc.StartNetworkProcess();

        // Created on this thread before the start returned, not later inside the capture task.
        var session = Assert.Single(captures.Created);
        Assert.Same(session, netProc.TestKernelSession);
        var task = netProc.PacketTask;
        Assert.NotNull(task);
        await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));

        netProc.TestSwitchAdapter("Ethernet", "id-eth", EthernetIp, NoIPv6);

        Assert.Single(captures.Created);
        Assert.Same(session, netProc.TestKernelSession);
        Assert.Equal(0, session.DisposeCount);
        Assert.Same(task, netProc.PacketTask);
        Assert.False(task!.IsCompleted);
        Assert.Equal("Ethernet", netProc.AdapterName);
        Assert.Equal("id-eth", netProc.CurrentAdapterId);
        Assert.True(netProc.TestHasDatabase);

        // Stopping for real is what stops the session, exactly once.
        netProc.EndNetworkProcess();
        Assert.Equal(1, session.DisposeCount);
        Assert.Null(netProc.TestKernelSession);
    }

    [Fact]
    public void Switch_RaisesDisconnectedThenTheNewAdapterAsTheStatus()
    {
        using var netProc = new NetworkProcess();
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);
        netProc.TestSetNetworkOnline("WiFi", "id-wifi");

        var statuses = new List<string>();
        netProc.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NetworkProcess.IsNetworkOnline))
                statuses.Add(netProc.IsNetworkOnline);
        };

        netProc.TestSwitchAdapter("Ethernet", "id-eth", EthernetIp, NoIPv6);

        // The same pair the UI saw when a switch restarted capture: it clears the old session, then rebases.
        Assert.Equal(["Disconnected", "Ethernet"], statuses);
        Assert.Equal("Ethernet", netProc.IsNetworkOnline);
    }

    // ---- Counting and database attribution across a switch ----

    [Fact]
    public void Switch_GivesTheOldAdaptersTailToTheUiAndTheDatabaseUnderItsName_AndLaterTrafficToTheNewOne()
    {
        using var networkType = new NetworkTypeScope(2);
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess { DbWriteErrorLogger = (message, _) => Assert.Fail(message) };
        using var service = new WindowsNetworkCaptureService(netProc);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);
        netProc.TestUseDatabase("WiFi", db);
        netProc.TestSetNetworkOnline("WiFi", "id-wifi");

        var batches = new List<(string Adapter, IReadOnlyList<ProcessTraffic> Traffic)>();
        service.TrafficBatchObserved += (_, e) => batches.Add((netProc.AdapterName, e.Traffic));

        // A normal tick on WiFi, then more WiFi traffic that no tick has taken yet: the tail.
        netProc.TestInvokeRecvProcess(Remote, Wifi, 400, "chrome");
        netProc.TestSpeedTick();
        netProc.TestInvokeRecvProcess(Remote, Wifi, 70, "chrome");
        netProc.TestInvokeSendProcess(Wifi, Remote, 5, "teams");
        Assert.Single(batches);

        netProc.TestSwitchAdapter("Ethernet", "id-eth", EthernetIp, NoIPv6);

        // The tail went to the UI as its own batch, still attributed to WiFi...
        Assert.Equal(2, batches.Count);
        Assert.Equal("WiFi", batches[1].Adapter);
        Assert.Contains(new ProcessTraffic("chrome", 70, 0), batches[1].Traffic);
        Assert.Contains(new ProcessTraffic("teams", 0, 5), batches[1].Traffic);

        // ...and to the database under WiFi, together with what the earlier tick had staged.
        Assert.Equal((470L, 0L), UsageFor(scope, "WiFi", "chrome"));
        Assert.Equal((0L, 5L), UsageFor(scope, "WiFi", "teams"));
        Assert.Null(UsageFor(scope, "Ethernet", "chrome"));
        Assert.Equal(1, scope.Scalar("SELECT COUNT(*) FROM Adapter WHERE Name = 'Ethernet'"));
        lock (netProc.PushToDBBuffer)
            Assert.Empty(netProc.PushToDBBuffer);

        // The filter moved with the switch: the old address no longer counts, the new one does.
        netProc.TestInvokeRecvProcess(Remote, Wifi, 999, "chrome");
        Assert.Empty(netProc.MyProcesses);
        netProc.TestInvokeRecvProcess(Remote, Ethernet, 100, "chrome");
        netProc.TestSpeedTick();

        Assert.Equal(3, batches.Count);
        Assert.Equal("Ethernet", batches[2].Adapter);
        Assert.Equal(new ProcessTraffic("chrome", 100, 0), Assert.Single(batches[2].Traffic));

        netProc.TestPushPendingToDb();
        Assert.Equal((100L, 0L), UsageFor(scope, "Ethernet", "chrome"));
        Assert.Equal((470L, 0L), UsageFor(scope, "WiFi", "chrome"));
    }

    [Fact]
    public void Switch_ResetsTheCountersAndTheSpeedBaseline_SoTheNextTickIsNotNegative()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = new NetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);
        netProc.TestSetNetworkOnline("WiFi", "id-wifi");
        long unit = SettingsManager.Current.NetworkSpeedFormat == 0 ? 8 : 1;

        netProc.TestInvokeRecvProcess(Remote, Wifi, 1000, "chrome");
        netProc.TestInvokeSendProcess(Wifi, Remote, 300, "chrome");
        netProc.TestSpeedTick();
        Assert.Equal(1000 * unit, netProc.DownloadSpeed);
        Assert.Equal(300 * unit, netProc.UploadSpeed);

        netProc.TestInvokeRecvProcess(Remote, Wifi, 200, "chrome");
        netProc.TestSwitchAdapter("Ethernet", "id-eth", EthernetIp, NoIPv6);

        Assert.Equal(0, netProc.CurrentSessionDownloadData);
        Assert.Equal(0, netProc.CurrentSessionUploadData);
        Assert.Equal(0, netProc.DownloadSpeed);
        Assert.Equal(0, netProc.UploadSpeed);

        // With the old baseline (1000) kept, this tick would report 100 - 1000 bytes.
        netProc.TestInvokeRecvProcess(Remote, Ethernet, 100, "chrome");
        netProc.TestSpeedTick();

        Assert.Equal(100 * unit, netProc.DownloadSpeed);
        Assert.Equal(0, netProc.UploadSpeed);
    }

    [Fact]
    public async Task Switch_BeforeTheDatabaseIsReady_KeepsTheTailForTheOldAdapterAndAddsTheNewAdapterOnceReady()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        var captures = new FakeCaptures();
        var gate = new SetupGate();
        using var netProc = NewProcess(captures);
        netProc.TestUseDatabaseFactory(gate.Factory);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);

        try
        {
            netProc.StartNetworkProcess();
            Assert.True(gate.Entered.Wait(TimeSpan.FromSeconds(10)));
            Stage(netProc, ("chrome", 50, 5));

            // The switch finds no database session: the tail waits in the retry queue under the OLD adapter.
            netProc.TestSwitchAdapter("Ethernet", "id-eth", EthernetIp, NoIPv6);
            Assert.False(netProc.TestHasDatabase);
            var queued = Assert.Single(netProc.TestQueuedBatches());
            Assert.Equal("WiFi", queued.Key);
            Assert.Equal((50L, 5L), queued.Value["chrome"]);
        }
        finally
        {
            gate.Release.Set();
        }

        await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(netProc.TestHasDatabase);

        // The setup made the WiFi rows it started with and then noticed the adapter had changed.
        Assert.Equal(1, scope.Scalar("SELECT COUNT(*) FROM Adapter WHERE Name = 'WiFi'"));
        Assert.Equal(1, scope.Scalar("SELECT COUNT(*) FROM Adapter WHERE Name = 'Ethernet'"));

        netProc.TestPushPendingToDb();
        Assert.Empty(netProc.TestQueuedBatches());
        Assert.Equal((50L, 5L), UsageFor(scope, "WiFi", "chrome"));
    }

    // ---- Capture starts before the database ----

    [Fact]
    public async Task Start_BeginsCaptureBeforeTheDatabaseIsReady_AndTheDataStaysBufferedUntilThen()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        var captures = new FakeCaptures();
        var gate = new SetupGate();
        using var netProc = NewProcess(captures);
        netProc.TestUseDatabaseFactory(gate.Factory);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);

        try
        {
            netProc.StartNetworkProcess();

            // Capture and the status are up although the database setup is still held.
            Assert.True(gate.Entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.Single(captures.Created);
            Assert.Equal("WiFi", netProc.IsNetworkOnline);
            Assert.False(netProc.TestHasDatabase);

            // A push with no session returns early and leaves the staged data where it is.
            Stage(netProc, ("chrome", 10, 1));
            netProc.TestPushPendingToDb();
            lock (netProc.PushToDBBuffer)
                Assert.Equal((10L, 1L), (netProc.PushToDBBuffer["chrome"]!.CurrentDataRecv, netProc.PushToDBBuffer["chrome"]!.CurrentDataSend));
        }
        finally
        {
            gate.Release.Set();
        }

        await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(netProc.TestHasDatabase);

        netProc.TestPushPendingToDb();
        lock (netProc.PushToDBBuffer)
            Assert.Empty(netProc.PushToDBBuffer);
        Assert.Equal((10L, 1L), UsageFor(scope, "WiFi", "chrome"));
    }

    [Fact]
    public async Task Start_SetsUpTheDatabaseInTheBackgroundAndStopReleasesIt()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var netProc = NewProcess(new FakeCaptures());
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);

        netProc.StartNetworkProcess();
        await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(netProc.TestHasDatabase);
        Assert.True(ApplicationDB.IsConnectionOpen);
        Assert.Equal(1, scope.Scalar("SELECT COUNT(*) FROM Adapter WHERE Name = 'WiFi'"));

        netProc.EndNetworkProcess();

        Assert.False(netProc.TestHasDatabase);
        Assert.False(ApplicationDB.IsConnectionOpen);
    }

    [Fact]
    public async Task Stop_DuringTheDatabaseSetup_ReleasesTheSessionTheSetupCreatesAfterwards()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        var captures = new FakeCaptures();
        var gate = new SetupGate();
        using var netProc = NewProcess(captures);
        netProc.TestUseDatabaseFactory(gate.Factory);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);

        try
        {
            netProc.StartNetworkProcess();
            Assert.True(gate.Entered.Wait(TimeSpan.FromSeconds(10)));
            Stage(netProc, ("chrome", 50, 5));

            // The stop comes while the setup is still opening the database.
            netProc.EndNetworkProcess();
            Assert.False(netProc.TestHasDatabase);
        }
        finally
        {
            gate.Release.Set();
        }

        await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));

        // The session the setup finished creating was not published and was disposed: nothing holds the
        // connection any more.
        Assert.False(netProc.TestHasDatabase);
        Assert.False(ApplicationDB.IsConnectionOpen);

        // What was counted before the stop isn't dropped: it waits, with its adapter, for the next session.
        var waiting = Assert.Single(netProc.TestQueuedBatches());
        Assert.Equal("WiFi", waiting.Key);
        Assert.Equal((50L, 5L), waiting.Value["chrome"]);
        Assert.Equal(1, captures.Created[0].DisposeCount);
    }

    // ---- A database setup that fails is retried ----

    [Fact]
    public async Task DatabaseSetupThatFails_IsRetriedAndTheBufferedDataIsWrittenUnderTheRightAdapter()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        var errors = new List<string>();
        using var netProc = NewProcess(new FakeCaptures(), errors);
        long now = 1_000_000;
        netProc.TestUseClock(() => Volatile.Read(ref now));
        int attempts = 0;
        netProc.TestUseDatabaseFactory(adapter =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
                throw new IOException("disk busy");

            return new ApplicationDB(adapter);
        });
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);

        netProc.StartNetworkProcess();
        var failed = netProc.TestDatabaseSetupTask!;
        await failed.WaitAsync(TimeSpan.FromSeconds(10));

        // The failure is logged once and the session has no database.
        Assert.False(netProc.TestHasDatabase);
        Assert.Contains("Database setup error", Assert.Single(errors));

        // Usage counted meanwhile stays buffered or queued, each with its adapter: a switch happens before the retry.
        Stage(netProc, ("chrome", 50, 5));
        netProc.TestSwitchAdapter("Ethernet", "id-eth", EthernetIp, NoIPv6);
        Stage(netProc, ("teams", 7, 7));

        // Too early: a push leaves everything where it is and doesn't retry yet.
        Volatile.Write(ref now, 1_000_000 + 29_999);
        netProc.TestPushPendingToDb();
        Assert.Equal(1, Volatile.Read(ref attempts));
        Assert.Same(failed, netProc.TestDatabaseSetupTask);
        lock (netProc.PushToDBBuffer)
            Assert.Single(netProc.PushToDBBuffer);

        // Due: the push starts the retry, which succeeds.
        Volatile.Write(ref now, 1_000_000 + 30_000);
        netProc.TestPushPendingToDb();
        Assert.NotSame(failed, netProc.TestDatabaseSetupTask);
        await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, Volatile.Read(ref attempts));
        Assert.True(netProc.TestHasDatabase);

        netProc.TestPushPendingToDb();

        Assert.Equal((50L, 5L), UsageFor(scope, "WiFi", "chrome"));
        Assert.Equal((7L, 7L), UsageFor(scope, "Ethernet", "teams"));
        Assert.Null(UsageFor(scope, "Ethernet", "chrome"));
        Assert.Empty(netProc.TestQueuedBatches());
        lock (netProc.PushToDBBuffer)
            Assert.Empty(netProc.PushToDBBuffer);
        Assert.Single(errors);
    }

    [Fact]
    public async Task Stop_DuringARetry_ReleasesTheSessionItCreatesAndNothingRetriesAfterwards()
    {
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var netProc = NewProcess(new FakeCaptures());
        long now = 1_000_000;
        netProc.TestUseClock(() => Volatile.Read(ref now));
        int attempts = 0;
        using var retryEntered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        netProc.TestUseDatabaseFactory(adapter =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
                throw new IOException("disk busy");

            retryEntered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(20)), "the retry was never released");
            return new ApplicationDB(adapter);
        });
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);

        try
        {
            netProc.StartNetworkProcess();
            await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(netProc.TestHasDatabase);

            Volatile.Write(ref now, 1_000_000 + 30_000);
            netProc.TestPushPendingToDb();
            Assert.True(retryEntered.Wait(TimeSpan.FromSeconds(10)));

            // The stop comes while the retry is still opening the database.
            netProc.EndNetworkProcess();
        }
        finally
        {
            release.Set();
        }

        await netProc.TestDatabaseSetupTask!.WaitAsync(TimeSpan.FromSeconds(10));

        // The session the retry finished was not published, and was disposed.
        Assert.False(netProc.TestHasDatabase);
        Assert.False(ApplicationDB.IsConnectionOpen);

        // A stopped session doesn't retry, however long it has been.
        Volatile.Write(ref now, 1_000_000 + 600_000);
        netProc.TestPushPendingToDb();
        Assert.Equal(2, Volatile.Read(ref attempts));
        Assert.False(netProc.TestHasDatabase);
    }

    // ---- Stopping saves the last partial second ----

    [Fact]
    public void Stop_SavesTheBytesCountedAfterTheLastTick_BeforeTheStatusSaysDisconnected()
    {
        using var networkType = new NetworkTypeScope(2);
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess { DbWriteErrorLogger = (message, _) => Assert.Fail(message) };
        using var service = new WindowsNetworkCaptureService(netProc);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);
        netProc.TestUseDatabase("WiFi", db);
        netProc.TestSetNetworkOnline("WiFi", "id-wifi");

        var events = new List<string>();
        service.TrafficBatchObserved += (_, e) =>
            events.Add("batch:" + string.Join(",", e.Traffic.Select(t => $"{t.Name}={t.Received}/{t.Sent}").Order()));
        netProc.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NetworkProcess.IsNetworkOnline))
                events.Add("status:" + netProc.IsNetworkOnline);
        };

        netProc.TestInvokeRecvProcess(Remote, Wifi, 400, "chrome");
        netProc.TestSpeedTick();
        netProc.TestInvokeRecvProcess(Remote, Wifi, 70, "chrome");
        netProc.TestInvokeSendProcess(Wifi, Remote, 5, "teams");

        netProc.EndNetworkProcess();

        // The tail went to the UI as a batch of its own, ahead of the status change that clears the session...
        Assert.Equal(
            ["batch:chrome=400/0", "batch:chrome=70/0,teams=0/5", "status:Disconnected"],
            events);

        // ...and to the database, together with what the tick had staged.
        Assert.Equal((470L, 0L), UsageFor(scope, "WiFi", "chrome"));
        Assert.Equal((0L, 5L), UsageFor(scope, "WiFi", "teams"));
        lock (netProc.PushToDBBuffer)
            Assert.Empty(netProc.PushToDBBuffer);
        Assert.False(netProc.TestHasDatabase);
    }

    [Fact]
    public void ServiceStop_SavesTheBytesCountedAfterTheLastTick()
    {
        using var networkType = new NetworkTypeScope(2);
        using var scope = new ApplicationDbScope(retentionDays: 60);
        using var db = scope.Start(Day1);
        using var netProc = new NetworkProcess { DbWriteErrorLogger = (message, _) => Assert.Fail(message) };
        using var service = new WindowsNetworkCaptureService(netProc);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);
        netProc.TestUseDatabase("WiFi", db);
        netProc.TestSetNetworkOnline("WiFi", "id-wifi");

        var batches = new List<IReadOnlyList<ProcessTraffic>>();
        service.TrafficBatchObserved += (_, e) => batches.Add(e.Traffic);

        netProc.TestInvokeRecvProcess(Remote, Wifi, 400, "chrome");
        netProc.TestSpeedTick();
        netProc.TestInvokeRecvProcess(Remote, Wifi, 70, "chrome");

        // Stop is what exit and Delete All use; the service must still be listening while the process shuts down.
        service.Stop();

        Assert.Equal(2, batches.Count);
        Assert.Equal(new ProcessTraffic("chrome", 70, 0), Assert.Single(batches[1]));
        Assert.Equal((470L, 0L), UsageFor(scope, "WiFi", "chrome"));
    }

    [Fact]
    public void Stop_WithNothingCountedSinceTheLastTick_RaisesNoExtraBatch()
    {
        using var networkType = new NetworkTypeScope(2);
        using var netProc = new NetworkProcess();
        using var service = new WindowsNetworkCaptureService(netProc);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);
        netProc.TestSetNetworkOnline("WiFi", "id-wifi");

        var batches = new List<IReadOnlyList<ProcessTraffic>>();
        service.TrafficBatchObserved += (_, e) => batches.Add(e.Traffic);

        netProc.TestInvokeRecvProcess(Remote, Wifi, 400, "chrome");
        netProc.TestSpeedTick();
        netProc.EndNetworkProcess();

        // Only the tick's batch: an idle stop is not a tick, so it must not add a spurious (empty) one.
        Assert.Single(batches);
    }

    // ---- Health check: a capture that ends on its own is restarted ----

    [Fact]
    public async Task CaptureThatEndsOnItsOwn_IsLoggedOnceAndTheKernelSessionRestarted()
    {
        var errors = new List<string>();
        var captures = new FakeCaptures { EndImmediately = true };
        using var netProc = NewProcess(captures, errors);

        netProc.TestStartCapture();
        await netProc.PacketTask!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(captures.Created);

        netProc.TestCheckCaptureHealth();

        Assert.Equal(2, captures.Created.Count);
        Assert.Same(captures.Created[1], netProc.TestKernelSession);
        Assert.Equal(1, captures.Created[0].DisposeCount);
        var logged = Assert.Single(errors);
        Assert.Contains("stopped unexpectedly", logged);
    }

    [Fact]
    public async Task RestartsAreSpacedOutAndTheFailureStreakIsLoggedOnlyOnce()
    {
        var errors = new List<string>();
        var captures = new FakeCaptures { EndImmediately = true };
        using var netProc = NewProcess(captures, errors);

        netProc.TestStartCapture();
        await netProc.PacketTask!.WaitAsync(TimeSpan.FromSeconds(5));
        netProc.TestCheckCaptureHealth();
        Assert.Equal(2, captures.Created.Count);

        // The restarted session dies at once too; the next attempts wait out the cooldown (5 checks).
        await netProc.PacketTask!.WaitAsync(TimeSpan.FromSeconds(5));
        for (int i = 0; i < 4; i++)
            netProc.TestCheckCaptureHealth();
        Assert.Equal(2, captures.Created.Count);

        netProc.TestCheckCaptureHealth();
        Assert.Equal(3, captures.Created.Count);
        Assert.Single(errors);
    }

    [Fact]
    public async Task AFailureAfterAHealthyStretch_IsLoggedAgain()
    {
        var errors = new List<string>();
        var captures = new FakeCaptures { EndImmediately = true };
        using var netProc = NewProcess(captures, errors);

        netProc.TestStartCapture();
        await netProc.PacketTask!.WaitAsync(TimeSpan.FromSeconds(5));
        captures.EndImmediately = false;
        netProc.TestCheckCaptureHealth();
        Assert.Single(errors);

        // A long run without trouble ends the streak...
        for (int i = 0; i < 70; i++)
            netProc.TestCheckCaptureHealth();
        Assert.Equal(2, captures.Created.Count);

        // ...so when something stops the session from outside later, that is logged and restarted again.
        captures.Created[1].Dispose();
        await netProc.PacketTask!.WaitAsync(TimeSpan.FromSeconds(5));
        netProc.TestCheckCaptureHealth();

        Assert.Equal(3, captures.Created.Count);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void CaptureThatCouldNotStart_IsLoggedOnceAndRetriedUntilItWorks()
    {
        var errors = new List<string>();
        var captures = new FakeCaptures { FailNextCreates = 2 };
        using var netProc = NewProcess(captures, errors);

        netProc.TestStartCapture();

        Assert.Empty(captures.Created);
        Assert.Null(netProc.TestKernelSession);
        Assert.Single(errors);

        // The first check retries at once and fails again quietly; the next attempt, 5 checks later, succeeds.
        for (int i = 0; i < 5; i++)
            netProc.TestCheckCaptureHealth();
        Assert.Empty(captures.Created);
        for (int i = 0; i < 5; i++)
            netProc.TestCheckCaptureHealth();

        Assert.Single(captures.Created);
        Assert.Same(captures.Created[0], netProc.TestKernelSession);
        Assert.Single(errors);
    }

    [Fact]
    public void AfterStop_TheHealthCheckDoesNotRestartCapture()
    {
        var errors = new List<string>();
        var captures = new FakeCaptures();
        using var netProc = NewProcess(captures, errors);
        netProc.TestApplySnapshot("WiFi", "id-wifi", WifiIp, NoIPv6);
        netProc.TestStartCapture();

        netProc.EndNetworkProcess();
        for (int i = 0; i < 10; i++)
            netProc.TestCheckCaptureHealth();

        Assert.Single(captures.Created);
        Assert.Equal(1, captures.Created[0].DisposeCount);
        Assert.Null(netProc.TestKernelSession);
        Assert.Empty(errors);
    }

    // ---- Helpers ----

    private static NetworkProcess NewProcess(FakeCaptures captures, List<string>? errors = null)
    {
        var netProc = new NetworkProcess
        {
            ErrorLogger = (message, _) => errors?.Add(message),
            DbWriteErrorLogger = (message, _) => errors?.Add(message),
        };
        netProc.TestUseKernelCapture(captures.Factory);
        return netProc;
    }

    private static void Stage(NetworkProcess netProc, params (string Name, long Received, long Sent)[] rows)
    {
        lock (netProc.PushToDBBuffer)
        {
            foreach (var (name, received, sent) in rows)
                netProc.PushToDBBuffer[name] = new MyProcess_Small(name, received, sent);
        }
    }

    private static (long Received, long Sent)? UsageFor(ApplicationDbScope scope, string adapter, string process)
    {
        using var connection = scope.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT pd.DataReceived, pd.DataSent FROM ProcessDate pd " +
            "JOIN Process p ON p.ID = pd.ProcessID JOIN Adapter a ON a.ID = pd.AdapterID " +
            "WHERE a.Name = @Adapter AND p.Name = @Process";
        command.Parameters.AddWithValue("@Adapter", adapter);
        command.Parameters.AddWithValue("@Process", process);
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0), reader.GetInt64(1)) : null;
    }

    /// <summary>Makes capture sessions that block in Process() until disposed, like a real one, and counts them.</summary>
    private sealed class FakeCaptures
    {
        private readonly object sync = new();

        public List<FakeSession> Created { get; } = [];

        /// <summary>Sessions created while this is set end on their own at once, like a session stopped from outside.</summary>
        public bool EndImmediately { get; set; }

        /// <summary>The next this-many creations throw, like a failed EnableKernelProvider.</summary>
        public int FailNextCreates { get; set; }

        public Func<PacketHandler, IKernelCaptureSession> Factory => _ =>
        {
            lock (sync)
            {
                if (FailNextCreates > 0)
                {
                    FailNextCreates--;
                    throw new InvalidOperationException("no free ETW session");
                }

                var session = new FakeSession(EndImmediately);
                Created.Add(session);
                return session;
            }
        };
    }

    private sealed class FakeSession(bool endImmediately) : IKernelCaptureSession
    {
        private readonly ManualResetEventSlim stopped = new();
        private int disposeCount;

        public int DisposeCount => Volatile.Read(ref disposeCount);

        public void Process()
        {
            if (!endImmediately)
                stopped.Wait();
        }

        public void Dispose()
        {
            Interlocked.Increment(ref disposeCount);
            stopped.Set();
        }
    }

    /// <summary>Holds the background database setup inside its factory until released.</summary>
    private sealed class SetupGate
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Release { get; } = new();

        public Func<string, ApplicationDB> Factory => adapter =>
        {
            Entered.Set();
            Assert.True(Release.Wait(TimeSpan.FromSeconds(20)), "the setup gate was never released");
            return new ApplicationDB(adapter);
        };
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
