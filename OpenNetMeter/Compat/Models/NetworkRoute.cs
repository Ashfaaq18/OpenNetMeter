using System;
using System.Net;
using System.Runtime.InteropServices;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Models;

/// <summary>
/// Asks Windows which interface it would route internet traffic through.
/// This is a routing-table lookup; nothing is sent on the network.
/// </summary>
internal static class NetworkRoute
{
    // Public IPv4 used as the routing probe. GetBestInterface only reads the routing table.
    private const string ProbeAddress = "8.8.8.8";

    /// <summary>
    /// Returns the interface index Windows would use to reach the internet,
    /// or null if the lookup fails (for example when there is no route).
    /// </summary>
    public static int? GetBestInterfaceIndex()
    {
        try
        {
            // IPAddr is the address bytes as laid out in memory (network byte order).
            uint destination = BitConverter.ToUInt32(IPAddress.Parse(ProbeAddress).GetAddressBytes(), 0);
            if (GetBestInterface(destination, out uint interfaceIndex) != 0)
                return null;

            return (int)interfaceIndex;
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to look up the best network route", ex);
            return null;
        }
    }

    [DllImport("iphlpapi.dll")]
    private static extern int GetBestInterface(uint dwDestAddr, out uint pdwBestIfIndex);
}
