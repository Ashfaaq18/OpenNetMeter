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

namespace WhereIsMyData.Models
{
    public class NetworkInfo : INotifyPropertyChanged
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;

        private HashSet<string> NetworkProfiles;

        private string isNetworkOnline;
        public string IsNetworkOnline
        {
            get { return isNetworkOnline; }
            set { isNetworkOnline = value; OnPropertyChanged("IsNetworkOnline");  }
        }

        public ulong TotalBytesRecv { get; set; }
        public ulong TotalBytesSend { get; set; }
        public NetworkInfo(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref)
        {
            dusvm = dusvm_ref;
            dudvm = dudvm_ref;
            TotalBytesRecv = 0;
            TotalBytesSend = 0;
            NetworkProfiles = new HashSet<string>();
            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);           
        }


        private void SetNetworkStatus(bool isOnline, bool init)
        {
            string adapterName = "";
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
                
            }
            else
                IsNetworkOnline = "Disconnected";

            Task.Run(async () => {
                while (isOnline)
                {
                    WriteFile(adapterName);
                    await Task.Delay(1000);
                }
            });
        }
        public void InitNetworkStatus()
        {
            SetNetworkStatus(NetworkInterface.GetIsNetworkAvailable(), true);
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            SetNetworkStatus(e.IsAvailable, false);
            if (e.IsAvailable)
            { 
                //start saving to file
            }
            else
            { 
                //stop saving to file
            }
        }


        private void ReadFile(string name)
        {
            try
            {
                using (FileStream stream = new FileStream(name + ".WIMD", FileMode.Open, FileAccess.Read))
                {
                    (ulong, ulong) data;
                    data = FileIO.ReadFile_AppInfo(dudvm.MyApps, stream);
                    //Total bytes recieved
                    (decimal, int) temp = ConvBytesToOther.SizeSuffix(data.Item1);
                    dusvm.TotalDownloadData = temp.Item1;
                    dusvm.SuffixOfTotalDownloadData = temp.Item2;
                    //Total bytes sent
                    temp = ConvBytesToOther.SizeSuffix(data.Item2);
                    dusvm.TotalUploadData = temp.Item1;
                    dusvm.SuffixOfTotalUploadData = temp.Item2;
                }
            }
            catch (Exception e) { Debug.WriteLine("Error: " + e.Message); }
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


        //calculate the total Bytes sent and recieved
        private void RecvProcess(int size)
        {
            TotalBytesRecv += (ulong)size;
            (decimal, int) temp = ConvBytesToOther.SizeSuffix(TotalBytesRecv);
            dusvm.CurrentSessionDownloadData = temp.Item1;
            dusvm.SuffixOfDownloadData = temp.Item2;
        }

        private void SendProcess(int size)
        {
            TotalBytesSend += (ulong)size;
            (decimal, int) temp = ConvBytesToOther.SizeSuffix(TotalBytesSend);
            dusvm.CurrentSessionUploadData = temp.Item1;
            dusvm.SuffixOfUploadData = temp.Item2;
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
