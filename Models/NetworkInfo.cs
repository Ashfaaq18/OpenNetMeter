using System;
using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using WhereIsMyData.ViewModels;
using System.Collections.Generic;
using System.Threading;

namespace WhereIsMyData.Models
{
    public class NetworkInfo : INotifyPropertyChanged
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;
        private CancellationTokenSource cts;
        private CancellationToken token;
        private string adapterName;
        private string isNetworkOnline;
        public string IsNetworkOnline
        {
            get { return isNetworkOnline; }
            set { isNetworkOnline = value; OnPropertyChanged("IsNetworkOnline");  }
        }

        private ulong currentBytesRecv;
        private ulong currentBytesSend;
        private ulong TotalBytesRecv;
        private ulong totalBytesSend;
        public NetworkInfo(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref)
        {
            dusvm = dusvm_ref;
            dudvm = dudvm_ref;
            currentBytesRecv = 0;
            currentBytesSend = 0;
            TotalBytesRecv = 0;
            totalBytesSend = 0;
            adapterName = "";
            cts = new CancellationTokenSource();
            token = cts.Token;
            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);           
        }


        private void SetNetworkStatus(bool isOnline)
        {
            if (isOnline)
            {
                // var watch = Stopwatch.StartNew();
                NetworkInterface[] networks = NetworkInterface.GetAllNetworkInterfaces();
                NetworkInterface activeAdapter = networks.First(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                    && x.OperationalStatus == OperationalStatus.Up
                    && x.Name.StartsWith("vEthernet") == false);

                adapterName = activeAdapter.Name;
                IsNetworkOnline = "Connected : " + adapterName;

                ReadFile(adapterName);

                Task.Run(async () =>
                {
                    try
                    {
                        while (!token.IsCancellationRequested)
                        {
                            WriteFile(adapterName);
                            await Task.Delay(1000, token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("Operation Cancelled");
                        cts.Dispose();
                        cts = new CancellationTokenSource();
                        token = cts.Token;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Critical error: " + ex.Message);
                    }
                });
            }
            else
            {
                IsNetworkOnline = "Disconnected";
                cts.Cancel();
            }
        }

        public void InitNetworkStatus()
        {
            SetNetworkStatus(NetworkInterface.GetIsNetworkAvailable());
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            SetNetworkStatus(e.IsAvailable);
        }


        private void ReadFile(string name)
        {
            try
            {
                using (FileStream stream = new FileStream(name + ".WIMD", FileMode.Open, FileAccess.Read))
                {
                    (ulong, ulong) data;
                    data = FileIO.ReadFile_AppInfo(dudvm.MyApps, stream);

                    TotalBytesRecv = data.Item1;
                    dusvm.TotalDownloadData.Conv(TotalBytesRecv);
                    totalBytesSend = data.Item2;
                    dusvm.TotalUploadData.Conv(totalBytesSend);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error: " + e.Message);
            }
        }

        private void WriteFile(string name)
        {
            try
            {
                using (FileStream stream = new FileStream(name + ".WIMD", FileMode.OpenOrCreate, FileAccess.Write))
                {
                    FileIO.WriteFile_AppInfo(dudvm.MyApps, stream);
                }
            }
            catch (Exception e) { Debug.WriteLine("Error: " + e.Message); }
        }

        public void CaptureNetworkPackets()
        {
            Task.Run(() =>
            {
                using (TraceEventSession kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName))
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

        //implement network speed monitor
        public void CaptureNetworkSpeed()
        {

        }

        //upload events
        private void Kernel_UdpIpSendIPV6(UpdIpV6TraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                SendProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
            }
        }

        private void Kernel_UdpIpSend(UdpIpTraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                SendProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
            }
        }

        private void Kernel_TcpIpSendIPV6(TcpIpV6SendTraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                SendProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
            }
        }

        private void Kernel_TcpIpSend(TcpIpSendTraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                SendProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, 0, obj.size);
            }
        }
 
        //download events    
        private void Kernel_UdpIpRecv(UdpIpTraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                RecvProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
            }
        }

        private void Kernel_UdpIpRecvIPV6(UpdIpV6TraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                RecvProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
            }
        }

        private void Kernel_TcpIpRecv(TcpIpTraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                RecvProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
            }
        }

        private void Kernel_TcpIpRecvIPV6(TcpIpV6TraceData obj)
        {
            if (!IPAddress.IsLoopback(obj.daddr))
            {
                RecvProcess(obj.size);
                dudvm.GetAppDataInfo(obj.ProcessName, obj.size, 0);
            }
        }


        //calculate the Bytes sent and recieved
        private void RecvProcess(int size)
        {
            TotalBytesRecv += (ulong)size;
            dusvm.TotalDownloadData.Conv(TotalBytesRecv);

            currentBytesRecv += (ulong)size;
            dusvm.CurrentSessionDownloadData.Conv(currentBytesRecv);

        }

        private void SendProcess(int size)
        {
            totalBytesSend += (ulong)size;
            dusvm.TotalUploadData.Conv(totalBytesSend);

            currentBytesSend += (ulong)size;
            dusvm.CurrentSessionUploadData.Conv(currentBytesSend);
        }


        //------property changers---------------//

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
