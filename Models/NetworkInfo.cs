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
using OpenNetMeter.ViewModels;
using System.Threading;
using ManagedNativeWifi;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Collections.Generic;

namespace OpenNetMeter.Models
{
    public class NetworkInfo : INotifyPropertyChanged
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;

        private string localIP;

        private ulong downloadSpeed;
        public ulong DownloadSpeed
        {
            get { return downloadSpeed; }
            set { downloadSpeed = value; OnPropertyChanged("DownloadSpeed"); }
        }
        private ulong uploadSpeed;
        public ulong UploadSpeed
        {
            get { return uploadSpeed; }
            set { uploadSpeed = value; OnPropertyChanged("UploadSpeed"); }
        }

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
        public NetworkInfo(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref)
        {
            dusvm = dusvm_ref;
            dudvm = dudvm_ref;

            DownloadSpeed = 0;
            UploadSpeed = 0;
            
            adapterName = "";

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            CaptureNetworkPackets();
        }

        private string GetLocalIP()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }

        public void InitConnection()
        {
            IsNetworkOnline = "Disconnected";
            dudvm.Profiles = new ObservableCollection<string>(FileIO.GetProfiles());
            if (dudvm.Profiles.Count > 0)
                dudvm.SelectedProfile = dudvm.Profiles[0];
            NetworkChange_NetworkAddressChanged(null, null);
        }

        
        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            string tempIP;
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface n in adapters)
            {
                if (n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    if (n.OperationalStatus == OperationalStatus.Up) //if there is a connection
                    {
                        tempIP = GetLocalIP(); //get assigned ip

                        IPInterfaceProperties adapterProperties = n.GetIPProperties();
                        if (adapterProperties.GatewayAddresses.FirstOrDefault() != null)
                        {
                            foreach (UnicastIPAddressInformation ip in adapterProperties.UnicastAddresses)
                            {
                                if (ip.Address.ToString() == tempIP)
                                {
                                    if(localIP == tempIP) //this is to prevent this event from firing multiple times during 1 connection change
                                        break;
                                    else
                                        localIP = tempIP;

                                    if (IsNetworkOnline != "Disconnected") //if there was already a connection available
                                        SetNetworkStatus(false); //reset the connection

                                    adapterName = n.Name;

                                    if (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                        adapterName += "(" + NativeWifi.EnumerateConnectedNetworkSsids().FirstOrDefault().ToString() + ")";

                                    SetNetworkStatus(true);
                                    CaptureNetworkSpeed();
                                    Debug.WriteLine(n.Name + " is up " + ", IP: " + ip.Address.ToString());
                                    break;
                                }
                            }
                        }

                    }
                }
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                localIP = "";
                Debug.WriteLine("No connection");
                SetNetworkStatus(false);
            }
        }

        private void SetNetworkStatus(bool isOnline)
        {
            if (isOnline)
            {
                IsNetworkOnline = "Connected : " + adapterName;
                //read saved data of adapter
                FileIO.ReadFile(ref dusvm, ref dudvm, adapterName, false);

                dudvm.Profiles = new ObservableCollection<string>(FileIO.GetProfiles()); //this statement should always be below FileIO.ReadFile, this registers the available saved profiles

                dudvm.CurrentConnection = adapterName;
                dudvm.SelectedProfile = adapterName;

                WriteToFile();
            }
            else //if network is disconnected
            {
                IsNetworkOnline = "Disconnected";

                if(cts_file != null)
                    cts_file.Cancel(); //stop writing to file
                if(cts_speed != null)
                    cts_speed.Cancel(); //stop calculating network speed

                //reset speed counters
                DownloadSpeed = 0;
                UploadSpeed = 0;

                dusvm.CurrentSessionDownloadData = 0;
                dusvm.CurrentSessionUploadData = 0;
                dusvm.TotalDownloadData = 0;
                dusvm.TotalUploadData = 0;
                dudvm.CurrentConnection = "";

                foreach (var row in dudvm.OnProfVM.MyProcesses.ToList())
                {
                    dudvm.OnProfVM.MyProcesses.Remove(row.Key);
                }
            }
        }

        public void ResetWriteFileAndSpeed()
        {
            string completePath = Path.Combine(FileIO.FolderPath(), adapterName + ".onm");

            if (IsNetworkOnline != "Disconnected")
            {
                //stop counters
                if (cts_file != null)
                    cts_file.Cancel(); //stop writing to file
                if (cts_speed != null)
                    cts_speed.Cancel(); //stop calculating network speed

                //reset speed counters
                DownloadSpeed = 0;
                UploadSpeed = 0;
                //recreate file
                FileIO.DeleteFile(completePath);
                FileIO.CreateFile(completePath);

                //restart write file and capturing speed
                SetNetworkStatus(true);
                CaptureNetworkSpeed();
            }
            else
            {
                //recreate file
                FileIO.DeleteFile(completePath);
                FileIO.CreateFile(completePath);
            }
        }
        public void WriteToFile()
        {
            //init tokens
            cts_file = new CancellationTokenSource();
            token_file = cts_file.Token;

            //start writing to file every second
            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Operation Started : Write file");
                    while (!token_file.IsCancellationRequested)
                    {
                        FileIO.WriteFile_MyProcess(dudvm.OnProfVM.MyProcesses, Path.Combine(FileIO.FolderPath(), adapterName + ".onm"));
                        await Task.Delay(1000, token_file);
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation Cancelled : Write file");
                    cts_file.Dispose();
                    cts_file = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
                }
            });
        }
        public void CaptureNetworkSpeed()
        {
            cts_speed = new CancellationTokenSource();
            token_speed = cts_speed.Token;

            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Operation Started : Network speed");
                    while (!token_speed.IsCancellationRequested)
                    {
                        ulong temp1 = dusvm.CurrentSessionDownloadData;
                        ulong temp2 = dusvm.CurrentSessionUploadData;
                        await Task.Delay(1000, token_speed);
                        DownloadSpeed= (dusvm.CurrentSessionDownloadData - temp1)*8;
                        UploadSpeed= (dusvm.CurrentSessionUploadData - temp2)*8;
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation Cancelled : Network speed");
                    cts_speed.Dispose();
                    cts_speed = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
                }
            });
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
            SendProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_UdpIpSend(UdpIpTraceData obj)
        {
            SendProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpSendIPV6(TcpIpV6SendTraceData obj)
        {
            SendProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpSend(TcpIpSendTraceData obj)
        {
            SendProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }
 
        //download events    
        private void Kernel_UdpIpRecv(UdpIpTraceData obj)
        {
            RecvProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_UdpIpRecvIPV6(UpdIpV6TraceData obj)
        {
            RecvProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpRecv(TcpIpTraceData obj)
        {
            RecvProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpRecvIPV6(TcpIpV6TraceData obj)
        {
            RecvProcessIPV6(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }


        //calculate the Bytes sent and recieved
        private void RecvProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            if(!IPAddress.IsLoopback(src) && !IPAddress.IsLoopback(dest) && ((src.ToString() == localIP) ^ (dest.ToString() == localIP)))
            {
                dusvm.TotalDownloadData += (ulong)size;
                dusvm.CurrentSessionDownloadData += (ulong)size;

                dudvm.GetAppDataInfo(name, size, 0);
            }
        }

        private void RecvProcessIPV6(IPAddress src, IPAddress dest, int size, string name)
        {
            if (!IPAddress.IsLoopback(src) && !IPAddress.IsLoopback(dest) && ((src.ToString() == localIP) ^ (dest.ToString() == localIP)))
            {
                dusvm.TotalDownloadData += (ulong)size;
                dusvm.CurrentSessionDownloadData += (ulong)size;

                dudvm.GetAppDataInfo(name, size, 0);
            }
        }

        private void SendProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            if (!IPAddress.IsLoopback(src) && !IPAddress.IsLoopback(dest) && ((src.ToString() == localIP) ^ (dest.ToString() == localIP)))
            {
                dusvm.TotalUploadData += (ulong)size;
                dusvm.CurrentSessionUploadData += (ulong)size;

                dudvm.GetAppDataInfo(name, 0, size);
            }
        }
        private void SendProcessIPV6(IPAddress src, IPAddress dest, int size, string name)
        {
            if (!IPAddress.IsLoopback(src) && !IPAddress.IsLoopback(dest) && ((src.ToString() == localIP) ^ (dest.ToString() == localIP)))
            {
                dusvm.TotalUploadData += (ulong)size;
                dusvm.CurrentSessionUploadData += (ulong)size;

                dudvm.GetAppDataInfo(name, 0, size);
            }
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
