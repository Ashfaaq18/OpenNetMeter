using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace OpenNetMeter.Models
{
    // Test-only helpers kept internal to avoid exposing implementation details publicly.
    public partial class NetworkProcess
    {
        internal void TestSetLocalIPs(byte[] ipv4, byte[] ipv6)
        {
            localIPv4 = ipv4;
            localIPv6 = ipv6;
        }

        internal void TestSetNetworkOnline(string isNetworkOnline, string adapterId = "")
        {
            currentAdapterId = adapterId;
            IsNetworkOnline = isNetworkOnline;
        }

        // Gives the process the database session a started capture would hold, without starting capture.
        internal void TestUseDatabase(string adapterName, ApplicationDB db)
        {
            AdapterName = adapterName;
            sessionDb = db;
        }

        internal bool TestHasDatabase => sessionDb != null;

        internal void TestPushPendingToDb() => PushPendingToDb();

        // What the retry queue holds: adapter -> process -> (received, sent).
        internal Dictionary<string, Dictionary<string, (long Received, long Sent)>> TestQueuedBatches()
        {
            lock (failedBatches)
            {
                return failedBatches.ToDictionary(
                    entry => entry.Key,
                    entry => new Dictionary<string, (long Received, long Sent)>(entry.Value));
            }
        }

        internal void TestInvokeRecvProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            ProcessPacket(src, dest, size, name, isRecv: true);
        }

        internal void TestInvokeSendProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            ProcessPacket(src, dest, size, name, isRecv: false);
        }

        // Makes the process track an adapter the way HandleNetworkChange does after finding one.
        internal void TestApplySnapshot(string adapterName, string adapterId, byte[] ipv4, byte[] ipv6)
        {
            ApplySnapshot(new NetworkSnapshot(adapterName, adapterId, ipv4, ipv6));
        }

        // The "different adapter" branch of HandleNetworkChange, with the snapshot supplied instead of read from the system.
        internal void TestSwitchAdapter(string adapterName, string adapterId, byte[] ipv4, byte[] ipv6)
        {
            SwitchAdapter(new NetworkSnapshot(adapterName, adapterId, ipv4, ipv6));
        }

        // One run of the one-second speed tick, including the capture health check.
        internal void TestSpeedTick() => SpeedTick();

        internal void TestCheckCaptureHealth() => CheckCaptureHealth();

        // Starts capture alone, without the periodic tasks (and so without their background health checks).
        internal void TestStartCapture() => CaptureNetworkPackets();

        // Replaces the real ETW session with a fake one, so no ETW session is started.
        internal void TestUseKernelCapture(Func<PacketHandler, IKernelCaptureSession> factory)
        {
            kernelCaptureFactory = factory;
        }

        internal object? TestKernelSession => kernelSession;

        // Replaces how the session database is opened, so a test can hold the background setup at a known point.
        internal void TestUseDatabaseFactory(Func<string, ApplicationDB> factory)
        {
            createSessionDb = factory;
        }

        internal Task? TestDatabaseSetupTask => dbSetupTask;

        // Replaces the millisecond clock the database-setup retry interval is measured with.
        internal void TestUseClock(Func<long> clock)
        {
            clockMilliseconds = clock;
        }
    }
}
