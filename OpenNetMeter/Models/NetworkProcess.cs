using System;
using System.Net;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using OpenNetMeter.Utilities;
using Windows.Networking.Connectivity;

namespace OpenNetMeter.Models
{
    public class NetworkProcess : IDisposable
    {
        //---------- private variables ------------//

        private const int OneSec = 1000;

        private readonly byte[] defaultIPv4;
        private readonly byte[] defaultIPv6;
        private byte[] localIPv4;
        private byte[] localIPv6;

        //variables to create seperate running threads for updating the network speed and db push
        private AsyncTask asyncTask_networkSpeed;
        private AsyncTask asyncTask_dbPush;

        //this is used to run the event tracing (kernelSession) in a seperate thread
        public Task? PacketTask;

        private TraceEventSession? kernelSession;

        public string AdapterName { get; private set; }

        //memory to store the process network details temporarily before updating the views.
        public Dictionary<string, MyProcess_Small?>? MyProcesses { get; private set; }
        public Dictionary<string, MyProcess_Small?>? MyProcessesBuffer { get; private set; }

        //memory to store the process network details temporarily before pushing to the database
        public Dictionary<string, MyProcess_Small?>? PushToDBBuffer { get; private set; }

        /// <summary>
        /// why use this 'IsBufferTime'? 
        /// during its true state, the Recv() in NetworkProcess stores the incoming data to the netProc.MyProcessesBuffer dictionary.
        /// while its storing there, netProc.MyProcesses data is extracted to parse. Once done, this boolean is set to false
        /// during the false state, the Recv() function stores data in the netProc.MyProcesses dictionary
        /// during this, netProc.MyProcessesBuffer data is extracted to parse. 
        /// </summary>
        public bool IsBufferTime { get; set; }

        public long CurrentSessionDownloadData;

        public long CurrentSessionUploadData;

        public long UploadSpeed;

        //---------- variables with property changers ------------//
        public long downloadSpeed;
        public long DownloadSpeed
        {
            get { return downloadSpeed; }
            set { downloadSpeed = value; OnPropertyChanged("DownloadSpeed"); }
        }

        private string isNetworkOnline = "error";
        public string IsNetworkOnline
        {
            get { return isNetworkOnline; }
            set { isNetworkOnline = value; OnPropertyChanged("IsNetworkOnline"); }
        }

        public NetworkProcess()
        {
            //initialize variables
            defaultIPv4 = new byte[]
            {
                0, 0, 0, 0
            };
            defaultIPv6 = new byte[]
            {
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0,
                0, 0, 0, 0
            };
            localIPv4 = defaultIPv4;
            localIPv6 = defaultIPv6;
            AdapterName = "";
            MyProcesses = new Dictionary<string, MyProcess_Small?>();
            MyProcessesBuffer = new Dictionary<string, MyProcess_Small?>();
            PushToDBBuffer = new Dictionary<string, MyProcess_Small?>();
            IsBufferTime = false;

            kernelSession = null;
            PacketTask = null;

            asyncTask_networkSpeed = new AsyncTask(1);
            asyncTask_dbPush = new AsyncTask(60);

            CurrentSessionUploadData = 0;
            CurrentSessionDownloadData = 0;
            UploadSpeed = 0;
            DownloadSpeed = 0;
        }

        /// <summary>
        /// call after subscribing to the property handlers in the MainWindowVM
        /// </summary>
        public void Initialize()
        {
            IsNetworkOnline = "Disconnected";

            //subscribe address network address change
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;

            //check for online network addresses
            NetworkChange_NetworkAddressChanged(null, null);
        }

        /// <summary>
        /// returns local IP (IPv4, IPv6)
        /// </summary>
        /// <returns></returns>
        private (byte[], byte[]) GetLocalIP()
        {
            byte[] tempv4 = defaultIPv4;
            byte[] tempv6 = defaultIPv6;

            // IPv6
            using (Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("2001:4860:4860::8888", 65530);
                    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                    if(endPoint != null)
                        tempv6 = endPoint.Address.GetAddressBytes();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            // IPv4
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                try
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
                    if(endPoint != null)
                        tempv4 = endPoint.Address.GetAddressBytes();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            return (tempv4, tempv6);
        }

        private void NetworkChange_NetworkAddressChanged(object? sender, EventArgs? e)
        {
            (byte[], byte[]) tempIP = (defaultIPv4, defaultIPv6);
            bool networkAvailable = false;
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
                                if (ByteArray.Compare(ip.Address.GetAddressBytes(), tempIP.Item1))
                                {
                                    if (localIPv4 == tempIP.Item1) //this is to prevent this event from firing multiple times during 1 connection change
                                        break;
                                    else
                                        localIPv4 = tempIP.Item1;

                                    networkAvailable = true;
                                    AdapterName = n.Name;
                                    if (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                        AdapterName += "(" + NetworkInformation.GetInternetConnectionProfile()?.WlanConnectionProfileDetails?.GetConnectedSsid() + ")";

                                    Debug.WriteLine(n.Name + " is up " + ", IP: " + ip.Address.ToString());
                                }
                                else if(ByteArray.Compare(ip.Address.GetAddressBytes(), tempIP.Item2))
                                {
                                    if (localIPv6 == tempIP.Item2) //this is to prevent this event from firing multiple times during 1 connection change
                                        break;
                                    else
                                        localIPv6 = tempIP.Item2;

                                    networkAvailable = true;
                                    AdapterName = n.Name;
                                    if (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                        AdapterName += "(" + NetworkInformation.GetInternetConnectionProfile()?.WlanConnectionProfileDetails?.GetConnectedSsid() + ")";

                                    Debug.WriteLine(n.Name + " is up " + ", IP: " + ip.Address.ToString());
                                }
                            }
                        }
                    }
                }
            }

            if (networkAvailable)
            {
                if (IsNetworkOnline == "Disconnected")
                    StartNetworkProcess();
                else if(IsNetworkOnline != AdapterName)
                {
                    EndNetworkProcess();
                    StartNetworkProcess();
                }
            }
            else
            {
                if(IsNetworkOnline != "Disconnected")
                    EndNetworkProcess();
            }

            //the ByteArrayCompare is used to detect virtual ethernet adapters escaping from a null,
            //adapterProperties.GatewayAddresses.FirstOrDefault() in the above foreach loop
            if (!NetworkInterface.GetIsNetworkAvailable() || ByteArray.Compare(tempIP.Item1, defaultIPv4))
            {
                localIPv4 = new byte[] { 0, 0, 0, 0 };
                Debug.WriteLine("No connection");
                if (isNetworkOnline != "Disconnected")
                    EndNetworkProcess();
            }
        }

        public void StartNetworkProcess()
        {
            using (ApplicationDB dB = new ApplicationDB(AdapterName))
            {
                if (dB.CreateTable() < 0)
                    Debug.WriteLine("Error: Create table");
                else
                {
                    dB.UpdateDatesInDB();
                }
            }

            CaptureNetworkPackets(); //start capturing packets
            asyncTask_networkSpeed.Task = CaptureNetworkSpeed(); //start logging the speed
            asyncTask_dbPush.Task = DBpush(); //start db push

            IsNetworkOnline = AdapterName;
        }

        public void EndNetworkProcess()
        {
            if (kernelSession != null)
            {
                kernelSession.Dispose();
                kernelSession = null;
            }

            if (PacketTask != null)
            {
                PacketTask.Dispose();
                PacketTask = null;
            }
            
            asyncTask_networkSpeed.Dispose();
            asyncTask_dbPush.Dispose();

            if (MyProcesses != null)
                MyProcesses.Clear();

            //reset speed counters
            CurrentSessionDownloadData = 0;
            CurrentSessionUploadData = 0;
            UploadSpeed = 0;
            DownloadSpeed = 0;

            IsNetworkOnline = "Disconnected";
        }

        //---------------------------------- Database push process ------------------------------------//

        private async Task DBpush()
        {
            asyncTask_dbPush.CancelToken = new CancellationTokenSource();
            try
            {
                Debug.WriteLine("Operation Started : DB push");
                while (await asyncTask_dbPush.Timer.WaitForNextTickAsync(asyncTask_dbPush.CancelToken.Token))
                {
#if DEBUG
                    Stopwatch sw1 = Stopwatch.StartNew();
#endif
                    if (PushToDBBuffer != null)
                    {
                        using (ApplicationDB dB = new ApplicationDB(AdapterName))
                        {
                            lock (PushToDBBuffer)
                            {
                                foreach (KeyValuePair<string, MyProcess_Small?> app in PushToDBBuffer)
                                {
                                    dB.PushToDB(app.Key, app.Value!.CurrentDataRecv, app.Value!.CurrentDataSend);
                                }

                                PushToDBBuffer.Clear();
                            }
                        }
                    }
#if DEBUG
                    sw1.Stop();
                    Debug.WriteLine($"elapsed time (DBpush): {sw1.ElapsedMilliseconds} | time {DateTime.Now.ToString("O")}");
#endif
                    //Debug.WriteLine($"current thread (CaptureNetworkSpeed): {Thread.CurrentThread.ManagedThreadId}");
                    //Debug.WriteLine($"networkProcess {DownloadSpeed}");
                }
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine($"db push token invoked: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"db push error: {ex.Message}");
            }
        }

        //----------------------------------------------------------------------------------------//

        //---------------------------------- NETWORK SPEED ---------------------------------------//

        private async Task CaptureNetworkSpeed()
        {
            asyncTask_networkSpeed.CancelToken = new CancellationTokenSource();
            try
            {
                long tempDownload = 0;
                long tempUpload = 0;
                Debug.WriteLine("Operation Started : Network speed");
                while (await asyncTask_networkSpeed.Timer.WaitForNextTickAsync(asyncTask_networkSpeed.CancelToken.Token))
                {
#if DEBUG
                    Stopwatch sw1 = Stopwatch.StartNew();
#endif
                    if(Properties.Settings.Default.NetworkSpeedFormat == 0)
                    {
                        UploadSpeed = (CurrentSessionUploadData - tempUpload) * 8;
                        DownloadSpeed = (CurrentSessionDownloadData - tempDownload) * 8;
                    }
                    else
                    {
                        UploadSpeed = (CurrentSessionUploadData - tempUpload);
                        DownloadSpeed = (CurrentSessionDownloadData - tempDownload);
                    }

                    tempUpload = CurrentSessionUploadData;
                    tempDownload = CurrentSessionDownloadData;
#if DEBUG
                    sw1.Stop();
                    Debug.WriteLine($"elapsed time (CaptureNetworkSpeed): {sw1.ElapsedMilliseconds} | time {DateTime.Now.ToString("O")}");
#endif
                    //Debug.WriteLine($"current thread (CaptureNetworkSpeed): {Thread.CurrentThread.ManagedThreadId}");
                    //Debug.WriteLine($"networkProcess {DownloadSpeed}");
                }
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine($"cancel speed token invoked: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Capture network speed error: {ex.Message}");
            }
        }

        //----------------------------------------------------------------------------------------//

        //---------------------------------- NETWORK PACKETS ------------------------------------//

        private void CaptureNetworkPackets()
        {
            Debug.WriteLine("Operation Started : Network capture");
            PacketTask = Task.Run(() =>
            {
                if(kernelSession == null)
                {
                    try
                    {
                        kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName);

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
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            });
        }

        //-----------------------------------------------------------------------------------------//

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
        private void RecvProcess(in IPAddress src, in IPAddress dest, in int size, in string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv4);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv4);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    Recv(name, size);
                }
            }
        }

        private void RecvProcessIPV6(in IPAddress src, in IPAddress dest, in int size, in string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv6);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv6);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    Recv(name, size);
                }
            }
        }

        private void SendProcess(in IPAddress src, in IPAddress dest, in int size, in string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv4);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv4);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    Send(name, size);
                }
            }
        }
        private void SendProcessIPV6(in IPAddress src, in IPAddress dest, in int size, in string name)
        {
            bool ipCompSrc = ByteArray.Compare(src.GetAddressBytes(), localIPv6);
            bool ipCompDest = ByteArray.Compare(dest.GetAddressBytes(), localIPv6);
            if (ipCompSrc ^ ipCompDest)
            {
                if (Properties.Settings.Default.NetworkType == 2 ? true : //both
                    Properties.Settings.Default.NetworkType == 1 ? (ipCompSrc ? !IsIPv4IPv6Private(dest) : !IsIPv4IPv6Private(src)) : //public
                    Properties.Settings.Default.NetworkType == 0 ? (ipCompSrc ?  IsIPv4IPv6Private(dest) :  IsIPv4IPv6Private(src)) : false) //private
                {
                    Send(name, size);
                }
            }
        }

        // 
        // How this works,
        // Recv() runs every second adding the process name and size of the packet to memory (MyProcesses Dictionary) temporarily.
        // this data is accessed and shown in the GUI. While accessing this data, the recv() adds the incoming process details to another memory container (MyProcessBuffer Dictionary)
        // When accessing the buffer memory to show in GUI, Recv() alternates and adds the process details to the initial memory container (MyProcesses)
        // The function alternates between these 2 memory containers.
        //
        private void Recv(string name, in int size)
        {
            CurrentSessionDownloadData += (long)size;

            if (name == "")
                name = "System";

            if(IsBufferTime)
            {
                lock(MyProcessesBuffer!)
                {
                    MyProcessesBuffer!.TryAdd(name, new MyProcess_Small(name, 0, 0));
                    MyProcessesBuffer[name]!.CurrentDataRecv += (long)size;
                }
            }
            else
            {
                lock(MyProcesses!)
                {
                    MyProcesses!.TryAdd(name, new MyProcess_Small(name, 0, 0));
                    MyProcesses[name]!.CurrentDataRecv += (long)size;
                }
            }

            //if (size == 0)
            //    Debug.WriteLine($"but whyyy {name} | recv");
        }

        private void Send(string name, in int size)
        {
            CurrentSessionUploadData += (long)size;

            if (name == "")
                name = "System";

            if (IsBufferTime)
            {
                lock (MyProcessesBuffer!) 
                {
                    MyProcessesBuffer!.TryAdd(name, new MyProcess_Small(name, 0, 0));
                    MyProcessesBuffer[name]!.CurrentDataSend += (long)size;
                }
                    
            }
            else
            {
                lock (MyProcesses!) 
                {
                    MyProcesses!.TryAdd(name, new MyProcess_Small(name, 0, 0));
                    MyProcesses[name]!.CurrentDataSend += (long)size;
                }
            }

            //if (size == 0)
            //    Debug.WriteLine($"but whyyy {name} | send");
        }

        private bool IsIPv4IPv6Private(IPAddress ip)
        {
            // Map back to IPv4 if mapped to IPv6, for example "::ffff:1.2.3.4" to "1.2.3.4".
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();

            // Checks loopback ranges for both IPv4 and IPv6.
            if (IPAddress.IsLoopback(ip)) return true;

            // IPv4
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return IsIPv4Private(ip.GetAddressBytes());

            // IPv6
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return ip.IsIPv6LinkLocal ||
#if NET6_0
                        ip.IsIPv6UniqueLocal ||
#endif
                        ip.IsIPv6SiteLocal;
            }

            throw new NotSupportedException(
                    $"IP address family {ip.AddressFamily} is not supported, expected only IPv4 (InterNetwork) or IPv6 (InterNetworkV6).");
        }

        private bool IsIPv4Private(byte[] ipv4Bytes)
        {
            // Link local (no IP assigned by DHCP): 169.254.0.0 to 169.254.255.255 (169.254.0.0/16)
            bool IsLinkLocal() => ipv4Bytes[0] == 169 && ipv4Bytes[1] == 254;

            // Class A private range: 10.0.0.0 – 10.255.255.255 (10.0.0.0/8)
            bool IsClassA() => ipv4Bytes[0] == 10;

            // Class B private range: 172.16.0.0 – 172.31.255.255 (172.16.0.0/12)
            bool IsClassB() => ipv4Bytes[0] == 172 && ipv4Bytes[1] >= 16 && ipv4Bytes[1] <= 31;

            // Class C private range: 192.168.0.0 – 192.168.255.255 (192.168.0.0/16)
            bool IsClassC() => ipv4Bytes[0] == 192 && ipv4Bytes[1] == 168;

            return IsLinkLocal() || IsClassA() || IsClassC() || IsClassB();
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public void Dispose()
        {
            if (IsNetworkOnline != "Disconnected")
                EndNetworkProcess();

            if(PushToDBBuffer != null)
            {
                if (PushToDBBuffer.Count > 0)
                {
                    using (ApplicationDB dB = new ApplicationDB(AdapterName))
                    {
                        lock (PushToDBBuffer)
                        {
                            foreach (KeyValuePair<string, MyProcess_Small?> app in PushToDBBuffer)
                            {
                                dB.PushToDB(app.Key, app.Value!.CurrentDataRecv, app.Value!.CurrentDataSend);
                            }

                            PushToDBBuffer.Clear();
                        }
                    }
                }
            }
        }
    }
}
