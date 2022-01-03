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
using System.Threading;
using ManagedNativeWifi;

namespace WhereIsMyData.Models
{
    public struct NetVar
    {
        public NetVar(ulong current = 0, ulong total = 0)
        {
            CurrentBytes = current;
            TotalBytes = total;
        }
        public ulong CurrentBytes { get; set; }
        public ulong TotalBytes { get; set; }
    }
    public class NetworkInfo : INotifyPropertyChanged
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;

        //token for write file
        private CancellationTokenSource cts_file;
        private CancellationToken token_file;

        //token for network speed
        private CancellationTokenSource cts_speed;
        private CancellationToken token_speed;

        private string adapterName;

        private string isNetworkOnline;
        public string IsNetworkOnline
        {
            get { return isNetworkOnline; }
            set { isNetworkOnline = value; OnPropertyChanged("IsNetworkOnline");  }
        }

        private NetVar Recv;
        private NetVar Send;
        public NetworkInfo(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref)
        {
            dusvm = dusvm_ref;
            dudvm = dudvm_ref;

            Recv = new NetVar();
            Send = new NetVar();

            adapterName = "";
            cts_file = new CancellationTokenSource();
            token_file = cts_file.Token;
            cts_speed = new CancellationTokenSource();
            token_speed = cts_speed.Token;

            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);           
        }


        private void SetNetworkStatus(bool isOnline)
        {
            if (isOnline)
            {
                //get adapter name
                NetworkInterface[] networks = NetworkInterface.GetAllNetworkInterfaces();
                NetworkInterface activeAdapter = networks.First(x => x.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                    && x.OperationalStatus == OperationalStatus.Up
                    && x.Name.StartsWith("vEthernet") == false);

                // -- future -- add option to filter when 2 connections are available
                if (activeAdapter.Name == "Wi-Fi" && NativeWifi.EnumerateConnectedNetworkSsids().Select(x => x.ToString()).Count() == 1)
                    adapterName = activeAdapter.Name + "__" + NativeWifi.EnumerateConnectedNetworkSsids().Select(x => x.ToString()).First();
                else
                    adapterName = activeAdapter.Name;

                IsNetworkOnline = "Connected : " + adapterName;  

                //read saved data of adapter
                ReadFile(adapterName);

                //start writing to file every second
                Task.Run(async () =>
                {
                    try
                    {
                        Debug.WriteLine("Operation Started : Write file");
                        while (!token_file.IsCancellationRequested)
                        {
                            WriteFile(adapterName);
                            await Task.Delay(1000, token_file);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("Operation Cancelled : Write file");
                        cts_file.Dispose();
                        cts_file = new CancellationTokenSource();
                        token_file = cts_file.Token;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Critical error: " + ex.Message);
                    }
                });
            }
            else //if network is disconnected
            {
                IsNetworkOnline = "Disconnected";
                cts_file.Cancel(); //cancel writing to file
                cts_speed.Cancel(); //cancel calculating network speed

                //reset speed
                dusvm.DownloadSpeed.Conv(0);
                dusvm.UploadSpeed.Conv(0);
            }
        }

        public void InitNetworkStatus()
        {
            SetNetworkStatus(NetworkInterface.GetIsNetworkAvailable());
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            SetNetworkStatus(e.IsAvailable);
            if(e.IsAvailable)
                CaptureNetworkSpeed();
        }


        private void ReadFile(string name)
        {
            try
            {
                using (FileStream stream = new FileStream(name + ".WIMD", FileMode.Open, FileAccess.Read))
                {
                    (ulong, ulong) data;
                    data = FileIO.ReadFile_AppInfo(dudvm.MyApps, stream);

                    Recv.TotalBytes = data.Item1;
                    dusvm.TotalDownloadData.Conv(Recv.TotalBytes);
                    Send.TotalBytes = data.Item2;
                    dusvm.TotalUploadData.Conv(Send.TotalBytes);
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
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Operation Started : Network speed");
                    while (!token_speed.IsCancellationRequested)
                    {
                        ulong temp1 = Recv.CurrentBytes;
                        ulong temp2 = Send.CurrentBytes;
                        await Task.Delay(1000, token_speed);
                        dusvm.DownloadSpeed.Conv(Recv.CurrentBytes - temp1);
                        dusvm.UploadSpeed.Conv(Send.CurrentBytes - temp2);
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation Cancelled : Network speed");
                    cts_speed.Dispose();
                    cts_speed = new CancellationTokenSource();
                    token_speed = cts_speed.Token;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
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


        //calculate the Bytes sent and recieved
        private void RecvProcess(int size)
        {
            Recv.TotalBytes += (ulong)size;
            dusvm.TotalDownloadData.Conv(Recv.TotalBytes);

            Recv.CurrentBytes += (ulong)size;
            dusvm.CurrentSessionDownloadData.Conv(Recv.CurrentBytes);

        }

        private void SendProcess(int size)
        {
            Send.TotalBytes += (ulong)size;
            dusvm.TotalUploadData.Conv(Send.TotalBytes);

            Send.CurrentBytes += (ulong)size;
            dusvm.CurrentSessionUploadData.Conv(Send.CurrentBytes);
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
