using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WhereIsMyData.Models
{
    public static class UDP_Table
    {
        public const int AF_INET = 2; // IP_v4 = System.Net.Sockets.AddressFamily.InterNetwork

        public const int AF_INET6 = 23; // IP_v6 = System.Net.Sockets.AddressFamily.InterNetworkV6

        public enum UDP_TABLE_CLASS
        {
            UDP_TABLE_BASIC,
            UDP_TABLE_OWNER_PID,
            UDP_TABLE_OWNER_MODULE
        }

        public struct MIB_UDPROW_OWNER_PID
        {
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public uint owningPid;

            public uint ProcessId
            {
                get { return owningPid; }
            }

            public System.Net.IPAddress LocalAddress
            {
                get { return new System.Net.IPAddress(localAddr); }
            }

            public ushort LocalPort
            {
                get
                {
                    return BitConverter.ToUInt16(new byte[2] { localPort[1], localPort[0] }, 0);
                }
            }
        }

        public struct MIB_UDPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 1)]
            public MIB_UDPROW_OWNER_PID[] table;
        }

        public static List<MIB_UDPROW_OWNER_PID> GetAllUDPConnections()

        {
            return GetUDPConnections<MIB_UDPROW_OWNER_PID, MIB_UDPTABLE_OWNER_PID>(AF_INET);
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedUdpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, UDP_TABLE_CLASS tblClass, uint reserved = 0);

        private static List<IPR> GetUDPConnections<IPR, IPT>(int ipVersion)//IPR = Row Type, IPT = Table Type

        {
            IPR[] tableRows;
            int buffSize = 0;

            var dwNumEntriesField = typeof(IPT).GetField("dwNumEntries");

            // how much memory do we need?
            uint ret = GetExtendedUdpTable(IntPtr.Zero, ref buffSize, true, ipVersion, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
            IntPtr tcpTablePtr = Marshal.AllocHGlobal(buffSize);

            try
            {
                ret = GetExtendedUdpTable(tcpTablePtr, ref buffSize, true, ipVersion, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);
                if (ret != 0)
                    return new List<IPR>();

                // get the number of entries in the table
                IPT table = (IPT)Marshal.PtrToStructure(tcpTablePtr, typeof(IPT));
                int rowStructSize = Marshal.SizeOf(typeof(IPR));
                uint numEntries = (uint)dwNumEntriesField.GetValue(table);

                // buffer we will be returning
                tableRows = new IPR[numEntries];

                IntPtr rowPtr = (IntPtr)((long)tcpTablePtr + 4);
                for (int i = 0; i < numEntries; i++)
                {
                    IPR tcpRow = (IPR)Marshal.PtrToStructure(rowPtr, typeof(IPR));
                    tableRows[i] = tcpRow;
                    rowPtr = (IntPtr)((long)rowPtr + rowStructSize);   // next entry
                }
            }
            finally
            {
                // Free the Memory
                Marshal.FreeHGlobal(tcpTablePtr);
            }
            return tableRows != null ? tableRows.ToList() : new List<IPR>();
        }
    }
}
