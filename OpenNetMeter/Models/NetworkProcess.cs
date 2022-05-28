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
    public class NetworkProcess
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;
        private MainWindowVM main;
        private MiniWidgetVM mwvm;

        private byte[] defaultIP; 
        private byte[] localIP;
        private byte[] localIPMask;

        //token for write file
        private CancellationTokenSource cts_file;
        private CancellationToken token_file;

        //token for network speed
        private CancellationTokenSource cts_speed;
        private CancellationToken token_speed;

        private string adapterName;

        private string isNetworkOnline;
        public NetworkProcess(DataUsageSummaryVM dusvm_ref, DataUsageDetailedVM dudvm_ref, MainWindowVM main_ref, MiniWidgetVM mwvm_ref)
        {
            defaultIP = new byte[] { 0, 0, 0, 0 };

            dusvm = dusvm_ref;
            dudvm = dudvm_ref;
            main = main_ref;
            mwvm = mwvm_ref;

            SetSpeed(0, 0);

            adapterName = "";

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            CaptureNetworkPackets();
        }

        private byte[] GetLocalIP()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint.Address.GetAddressBytes();
                }
                catch(Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                return new byte[] { 0, 0, 0, 0 };
            }
        }

        private bool IsLocalTraffic(ReadOnlySpan<byte> src, ReadOnlySpan<byte> srcMask, ReadOnlySpan<byte> dest)
        {
            if(src.Length == srcMask.Length  && srcMask.Length == dest.Length)
            {
                int tempCount = 0;
                for (int i = 0; i < src.Length; i++)
                {
                    //Debug.WriteLine("IPS: " + (src[i] & srcMask[i]) + "," + (dest[i] & srcMask[i]));
                    if ((src[i] & srcMask[i]) == (dest[i] & srcMask[i]))
                        tempCount++;
                    else
                        return false;
                }
                if (tempCount == src.Length)
                    return true;
            }
            return false;
        }

        private bool ByteArrayCompare(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
        {
            return a1.SequenceEqual(a2);
        }

        public void InitConnection()
        {
            isNetworkOnline = "Disconnected";
            main.NetworkStatus = isNetworkOnline;
            dudvm.Profiles = new ObservableCollection<string>();
            //if (dudvm.Profiles.Count > 0)
               // dudvm.SelectedProfile = dudvm.Profiles[0];
            NetworkChange_NetworkAddressChanged(null, null);
        }

        
        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            byte[] tempIP = defaultIP;
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
                                if (ByteArrayCompare(ip.Address.GetAddressBytes(), tempIP))
                                {
                                    if (localIP == tempIP) //this is to prevent this event from firing multiple times during 1 connection change
                                        break;
                                    else
                                    {
                                        localIP = tempIP;
                                        localIPMask = ip.IPv4Mask.GetAddressBytes();
                                        //Debug.WriteLine("temp: " + (localIP[0] & localIPMask[0]) + "," + (localIP[1] & localIPMask[1]) + ","+ (localIP[2] & localIPMask[2]) + ","+ (localIP[3] & localIPMask[3]) + ",");
                                    }

                                    if (isNetworkOnline != "Disconnected") //if there was already a connection available
                                        SetNetworkStatus(false); //reset the connection

                                    adapterName = n.Name;

                                    if (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                        adapterName += "(" + NativeWifi.EnumerateConnectedNetworkSsids().FirstOrDefault().ToString() + ")";

                                    SetNetworkStatus(true);
                                    Debug.WriteLine(n.Name + " is up " + ", IP: " + ip.Address.ToString());
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            //the ByteArrayCompare is used to detect virtual ethernet adapters escaping from a null,
            //adapterProperties.GatewayAddresses.FirstOrDefault() in the above foreach loop
            if (!NetworkInterface.GetIsNetworkAvailable() || ByteArrayCompare(tempIP, defaultIP))
            {
                localIP = new byte[]{0,0,0,0};
                Debug.WriteLine("No connection");
                if (isNetworkOnline != "Disconnected")
                    SetNetworkStatus(false);
            }
        }

        public void SetNetworkStatus(bool isOnline)
        {
            if (isOnline)
            {
                isNetworkOnline = "Connected : " + adapterName;
                dudvm.CurrentConnection = adapterName;

                CaptureNetworkSpeed(); //start logging the speed
            }
            else //if network is disconnected
            {
                isNetworkOnline = "Disconnected";

                if(cts_file != null)
                    cts_file.Cancel(); //stop writing to file
                if(cts_speed != null)
                    cts_speed.Cancel(); //stop calculating network speed

                //reset speed counters
                SetSpeed(0, 0);

                dusvm.CurrentSessionDownloadData = 0;
                dusvm.CurrentSessionUploadData = 0;
                dusvm.TotalDownloadData = 0;
                dusvm.TotalUploadData = 0;
                dudvm.CurrentConnection = "";

                foreach (var row in dudvm.MyProcesses.ToList())
                {
                    dudvm.MyProcesses.Remove(row.Key);
                }
            }

            main.NetworkStatus = isNetworkOnline;
        }

        private void SetSpeed(ulong download, ulong upload)
        {
            main.DownloadSpeed = download;
            main.UploadSpeed = upload;

            dusvm.Graph.DownloadSpeed = download;
            dusvm.Graph.UploadSpeed = upload;

            mwvm.DownloadSpeed = download;
            mwvm.UploadSpeed = upload;
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

                        SetSpeed((dusvm.CurrentSessionDownloadData - temp1) * 8, (dusvm.CurrentSessionUploadData - temp2) * 8);
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
            if(ByteArrayCompare(src.GetAddressBytes() , localIP) ^ ByteArrayCompare(dest.GetAddressBytes() , localIP))
            {
                if (Properties.Settings.Default.NetworkType == 2? true: //both
                    Properties.Settings.Default.NetworkType == 1? !IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()): //external
                    Properties.Settings.Default.NetworkType == 0?  IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()):false) //local
                {
                    dusvm.TotalDownloadData += (ulong)size;
                    dusvm.CurrentSessionDownloadData += (ulong)size;

                    mwvm.CurrentSessionDownloadData = dusvm.CurrentSessionDownloadData;

                    dudvm.GetAppDataInfo(name, size, 0);
                }
            }
        }

        private void RecvProcessIPV6(IPAddress src, IPAddress dest, int size, string name)
        {   
            if (ByteArrayCompare(src.GetAddressBytes(), localIP) ^ ByteArrayCompare(dest.GetAddressBytes(), localIP))
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? !IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()) : //external
                    Properties.Settings.Default.NetworkType == 0 ?  IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()) : false) //local
                {
                    dusvm.TotalDownloadData += (ulong)size;
                    dusvm.CurrentSessionDownloadData += (ulong)size;

                    mwvm.CurrentSessionDownloadData = dusvm.CurrentSessionDownloadData;

                    dudvm.GetAppDataInfo(name, size, 0);
                }
            }
        }

        private void SendProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            if (ByteArrayCompare(src.GetAddressBytes(), localIP) ^ ByteArrayCompare(dest.GetAddressBytes(), localIP))
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? !IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()) : //external
                    Properties.Settings.Default.NetworkType == 0 ?  IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()) : false) //local
                {
                    dusvm.TotalUploadData += (ulong)size;
                    dusvm.CurrentSessionUploadData += (ulong)size;

                    mwvm.CurrentSessionUploadData = dusvm.CurrentSessionUploadData;

                    dudvm.GetAppDataInfo(name, 0, size);
                }
            }
        }
        private void SendProcessIPV6(IPAddress src, IPAddress dest, int size, string name)
        {
            if (ByteArrayCompare(src.GetAddressBytes(), localIP) ^ ByteArrayCompare(dest.GetAddressBytes(), localIP))
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? !IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()) : //external
                    Properties.Settings.Default.NetworkType == 0 ?  IsLocalTraffic(src.GetAddressBytes(), localIPMask, dest.GetAddressBytes()) : false) //local
                {
                    dusvm.TotalUploadData += (ulong)size;
                    dusvm.CurrentSessionUploadData += (ulong)size;

                    mwvm.CurrentSessionUploadData = dusvm.CurrentSessionUploadData;

                    dudvm.GetAppDataInfo(name, 0, size);
                }
            }
        }
    }
}
