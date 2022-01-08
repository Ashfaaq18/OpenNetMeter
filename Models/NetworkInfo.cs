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
    public class NetworkInfo : INotifyPropertyChanged
    {
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;

        private IPAddressRange IP_10_;
        private IPAddressRange IP_172_16;
        private IPAddressRange IP_192_168;

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

            IP_10_ = new IPAddressRange(IPAddress.Parse("10.0.0.0"), IPAddress.Parse("10.255.255.255"));
            IP_172_16 = new IPAddressRange(IPAddress.Parse("172.16.0.0"), IPAddress.Parse("172.31.255.255"));
            IP_192_168 = new IPAddressRange(IPAddress.Parse("192.168.0.0"), IPAddress.Parse("192.168.255.255"));

            DownloadSpeed = 0;
            UploadSpeed = 0;

            adapterName = "";

            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);

        }

        public static bool IsAdminMode()
        {
            if (TraceEventSession.IsElevated() != true)
                return false;
            else
                return true;
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
                dusvm.TotalUsageText = "Total data usage of " + adapterName + " since : ";

                //read saved data of adapter
                ReadFile();

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
                            WriteFile();
                            await Task.Delay(1000, token_file);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("Operation Cancelled : Write file");
                        cts_file.Dispose(); 
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

                if(cts_file != null)
                    cts_file.Cancel(); //stop writing to file
                if(cts_speed != null)
                    cts_speed.Cancel(); //stop calculating network speed

                //reset speed counters
                DownloadSpeed=0;
                UploadSpeed=0;
            }
        }

        public void InitNetworkStatus()
        {
            SetNetworkStatus(NetworkInterface.GetIsNetworkAvailable());
        }

        public void ResetWriteFileAndSpeed()
        {
            if (NetworkInterface.GetIsNetworkAvailable())
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
                RecreateFile();
                //restart write file and capturing speed
                SetNetworkStatus(true);
                CaptureNetworkSpeed();
            }
            else
            {
                //recreate file
                RecreateFile();
            }
        }

        private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            SetNetworkStatus(e.IsAvailable);
            if(e.IsAvailable)
                CaptureNetworkSpeed();
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

        private bool IsPrivateIP(IPAddress ip)
        {
            return IP_10_.IsInRange(ip) || IP_172_16.IsInRange(ip) || IP_192_168.IsInRange(ip);
        }

        //upload events
        private void Kernel_UdpIpSendIPV6(UpdIpV6TraceData obj)
        {
            SendProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_UdpIpSend(UdpIpTraceData obj)
        {
            SendProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpSendIPV6(TcpIpV6SendTraceData obj)
        {
            SendProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
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
            RecvProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpRecv(TcpIpTraceData obj)
        {
            RecvProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }

        private void Kernel_TcpIpRecvIPV6(TcpIpV6TraceData obj)
        {
            RecvProcess(obj.saddr, obj.daddr, obj.size, obj.ProcessName);
        }


        //calculate the Bytes sent and recieved
        private void RecvProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            if(!IPAddress.IsLoopback(src) && !IPAddress.IsLoopback(dest) && !(IsPrivateIP(src) && IsPrivateIP(dest)) )
            {
                //Debug.WriteLine(src + "," + dest);
                dusvm.TotalDownloadData += (ulong)size;
                dusvm.CurrentSessionDownloadData += (ulong)size;

                dudvm.GetAppDataInfo(name, size, 0);
            }
        }

        private void SendProcess(IPAddress src, IPAddress dest, int size, string name)
        {
            if (!IPAddress.IsLoopback(src) && !IPAddress.IsLoopback(dest) && !(IsPrivateIP(src) && IsPrivateIP(dest)))
            {
                dusvm.TotalUploadData += (ulong)size;
                dusvm.CurrentSessionUploadData += (ulong)size;

                dudvm.GetAppDataInfo(name, 0, size);
            }
        }

        private string path;
        private string filename;
        private string pathString;
        //file stuff
        public void ReadFile()
        {
            path = "Profiles";
            filename = adapterName + ".WIMD";
            pathString = System.IO.Path.Combine(path, filename);
            try
            {
                // Try to create the directory.
                DirectoryInfo di = Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                Debug.WriteLine("The process failed: {0}", e.ToString());
            }

            if (File.Exists(pathString))
            {
                try
                {
                    using (FileStream stream = new FileStream(pathString, FileMode.Open, FileAccess.Read))
                    {
                        (ulong, ulong) data;
                        data = FileIO.ReadFile_AppInfo(dudvm.MyApps, stream);

                        dusvm.TotalDownloadData = data.Item1;
                        dusvm.TotalUploadData = data.Item2;

                        DateTime dateTime = File.GetCreationTime(pathString);
                        dusvm.Date = dateTime.ToShortDateString() + " , " + dateTime.ToShortTimeString();
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Cant Read: " + e.Message);
                }
            }
            else
            {
                File.Create(pathString);
                DateTime dateTime = File.GetCreationTime(pathString);
                dusvm.Date = dateTime.ToShortDateString() + " , " + dateTime.ToShortTimeString();
            }
        }

        private void WriteFile()
        {
            try
            {
                using (FileStream stream = new FileStream(pathString, FileMode.Open, FileAccess.Write))
                {
                    FileIO.WriteFile_AppInfo(dudvm.MyApps, stream);
                }
            }
            catch (Exception e) { Debug.WriteLine("Cant Write: " + e.Message); }
        }

        public void RecreateFile()
        {
            try
            {
                File.Delete(pathString);
                var file = File.Create(pathString);
                file.Close();
                //File.SetCreationTime(adapterName + ".WIMD", DateTime.Now);
            }
            catch(Exception ex) { Debug.WriteLine("Cant create: " + ex.Message); }
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
