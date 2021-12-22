﻿
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
        public ulong TotalBytesRecv { get; set; }
        public ulong TotalBytesSend { get; set; }
        public NetworkInfo(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref)
        {
            dusvm = dusvm_ref;
            dudvm = dudvm_ref;
            TotalBytesRecv = 0;
            TotalBytesSend = 0;

            Task.Run(() =>
            {
                using (kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName))
                {
                    kernelSession.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
                    kernelSession.Source.Kernel.TcpIpRecv += Kernel_TcpIpRecv;
                    kernelSession.Source.Kernel.TcpIpRecvIPV6 += Kernel_TcpIpRecvIPV6;
                    kernelSession.Source.Kernel.UdpIpRecv += Kernel_UdpIpRecv;
                    kernelSession.Source.Kernel.UdpIpRecvIPV6 += Kernel_UdpIpRecvIPV6;

                    kernelSession.Source.Kernel.TcpIpSend += Kernel_TcpIpSend;
                    kernelSession.Source.Kernel.TcpIpSendIPV6 += Kernel_TcpIpSendIPV6;
                    kernelSession.Source.Kernel.UdpIpSend += Kernel_UdpIpSend;
                    kernelSession.Source.Kernel.UdpIpSendIPV6 += Kernel_UdpIpSendIPV6;

                    kernelSession.Source.Process();
                }
            });
            
        }
        ~NetworkInfo()
        {
            kernelSession.Dispose();
        }

        //upload events
        private void Kernel_UdpIpSendIPV6(UpdIpV6TraceData obj)
        {
            SendProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
        }

        private void Kernel_UdpIpSend(UdpIpTraceData obj)
        {
            SendProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
        }

        private void Kernel_TcpIpSendIPV6(TcpIpV6SendTraceData obj)
        {
            SendProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
        }

        private void Kernel_TcpIpSend(TcpIpSendTraceData obj)
        {
            SendProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
        }
 
        //download events    
        private void Kernel_UdpIpRecv(UdpIpTraceData obj)
        {
            RecvProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
        }

        private void Kernel_UdpIpRecvIPV6(UpdIpV6TraceData obj)
        {
            RecvProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
        }

        private void Kernel_TcpIpRecv(TcpIpTraceData obj)
        {
            RecvProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
        }

        private void Kernel_TcpIpRecvIPV6(TcpIpV6TraceData obj)
        {
            RecvProcess(obj.size);
            dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
        }

        private void RecvProcess(int size)
        {
            TotalBytesRecv += (ulong)size;
            (decimal, int) temp = ConvBytesToOther.SizeSuffix(TotalBytesRecv);
            dusvm.CurrentSessionDownloadData = temp.Item1;
            dusvm.SuffixOfDownloadData = temp.Item2;
            //dusvm.CurrentSessionDownloadData = TotalBytesRecv / (1024);
        }

        private void SendProcess(int size)
        {
            TotalBytesSend += (ulong)size;
            (decimal, int) temp = ConvBytesToOther.SizeSuffix(TotalBytesSend);
            dusvm.CurrentSessionUploadData = temp.Item1;
            dusvm.SuffixOfUploadData = temp.Item2;
            // dusvm.CurrentSessionUploadData = TotalBytesSend / (1024);
        }



    }
}