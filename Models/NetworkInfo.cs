
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using WhereIsMyData.ViewModels;

namespace WhereIsMyData.Models
{
    public class NetworkInfo
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;
        private TraceEventSession kernelSession;
        private Stopwatch watch;
        private double elapsedTime;
        public NetworkInfo(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref)
        {
            dusvm = dusvm_ref;
            dudvm = dudvm_ref;
            TotalBytesRecv = 0;
            elapsedTime = 0;

            Task.Run(() =>
            {
                using (kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName))
                {
                    kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
                    kernelSession.Source.Kernel.TcpIpRecv += Kernel_TcpIpRecv;
                    kernelSession.Source.Kernel.TcpIpRecvIPV6 += Kernel_TcpIpRecvIPV6;
                    kernelSession.Source.Kernel.UdpIpRecv += Kernel_UdpIpRecv;
                    kernelSession.Source.Kernel.UdpIpRecvIPV6 += Kernel_UdpIpRecvIPV6;

                    kernelSession.Source.Process();
                }
            });
            
        }

        ~NetworkInfo ()
        {
            kernelSession.Dispose();
        }


        public ulong TotalBytesRecv { get; set; }
        private void Kernel_UdpIpRecv(UdpIpTraceData obj)
        {
            dudvm.EditProcessInfo(RecvProcess((ulong)obj.size), obj.ProcessName, (ulong)obj.size);
        }

        private void Kernel_UdpIpRecvIPV6(UpdIpV6TraceData obj)
        {
            dudvm.EditProcessInfo(RecvProcess((ulong)obj.size), obj.ProcessName, (ulong)obj.size);
        }

        private void Kernel_TcpIpRecv(TcpIpTraceData obj)
        {
            dudvm.EditProcessInfo( RecvProcess((ulong)obj.size), obj.ProcessName, (ulong)obj.size);
        }

        private void Kernel_TcpIpRecvIPV6(TcpIpV6TraceData obj)
        {
            dudvm.EditProcessInfo(RecvProcess((ulong)obj.size), obj.ProcessName, (ulong)obj.size);
        }

        private double RecvProcess(ulong size)
        {
            watch = Stopwatch.StartNew();
            TotalBytesRecv += size;
            dusvm.CurrentSessionData = TotalBytesRecv / (1024);
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }

    }
}
