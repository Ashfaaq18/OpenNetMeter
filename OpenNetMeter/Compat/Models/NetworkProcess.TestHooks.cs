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
