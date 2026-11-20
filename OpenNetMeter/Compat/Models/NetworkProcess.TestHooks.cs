using System.Collections.Generic;
using System.Linq;
using System.Net;

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
    }
}
