using System;
using System.Net;
using System.Runtime.Versioning;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Models
{
    /// <summary>Receives one captured packet: the addresses, its size and the name of the process it belongs to.</summary>
    internal delegate void PacketHandler(IPAddress src, IPAddress dest, int size, string processName, bool isRecv);

    /// <summary>
    /// A running packet capture. An interface so tests can count starts and stops without a real ETW session,
    /// which needs administrator rights and is machine-wide.
    /// </summary>
    internal interface IKernelCaptureSession : IDisposable
    {
        /// <summary>Delivers packets to the handler and blocks until the session is disposed or fails.</summary>
        void Process();
    }

    /// <summary>
    /// ETW kernel session that traces every TCP/UDP send and receive on the machine. Creating it starts the
    /// session, so a failure (no administrator rights, no free session) surfaces from the constructor on the
    /// calling thread instead of inside the capture task.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class EtwKernelCaptureSession : IKernelCaptureSession
    {
        private readonly TraceEventSession session;

        public EtwKernelCaptureSession(string sessionName, PacketHandler onPacket)
        {
            // The default (Create) options stop and recreate a session of the same name that is already running,
            // so an orphan left by a crash is cleaned up here; no separate GetActiveSession(...).Stop() is needed.
            session = new TraceEventSession(sessionName);
            try
            {
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
                StartupTiming.LogEtwEnabled();

                // Subscribe to all TCP/UDP send/receive events
                // All events funnel through the handler (NetworkProcess.ProcessPacket) for unified handling
                var kernel = session.Source.Kernel;

                // Receive events (download)
                kernel.TcpIpRecv += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);
                kernel.TcpIpRecvIPV6 += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);
                kernel.UdpIpRecv += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);
                kernel.UdpIpRecvIPV6 += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: true);

                // Send events (upload)
                kernel.TcpIpSend += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);
                kernel.TcpIpSendIPV6 += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);
                kernel.UdpIpSend += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);
                kernel.UdpIpSendIPV6 += data => onPacket(data.saddr, data.daddr, data.size, data.ProcessName, isRecv: false);
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        /// <summary>Blocks until the session is disposed.</summary>
        public void Process() => session.Source.Process();

        public void Dispose() => session.Dispose();
    }
}
