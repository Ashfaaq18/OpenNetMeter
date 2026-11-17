using System;
using System.Net;
using System.Runtime.InteropServices;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Models;

/// <summary>
/// Asks Windows which interface it would route internet traffic through, and reports routing table changes.
/// The lookup is a routing-table read; nothing is sent on the network.
/// </summary>
internal static class NetworkRoute
{
    // IPv4 address used as the routing probe: 203.0.113.1 is in TEST-NET-3 (RFC 5737, documentation range).
    // It is never routed specifically, so the lookup follows the default/catch-all routes (including a
    // full-tunnel VPN's 0.0.0.0/1 + 128.0.0.0/1) instead of being captured by a split-tunnel route for a
    // well-known public address such as 8.8.8.8/32. GetBestInterface only reads the routing table;
    // nothing is sent.
    private const string ProbeAddress = "203.0.113.1";

    private const ushort AfInet = 2;

    /// <summary>
    /// Returns the interface index Windows would use to reach the internet (the interface that
    /// carries the default/catch-all route), or null if the lookup fails (for example when there is no route).
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

    /// <summary>
    /// Calls <paramref name="onChange"/> whenever an IPv4 route is added, changed or removed.
    /// IPv4 only: the probe in <see cref="GetBestInterfaceIndex"/> is an IPv4 lookup, so IPv6 route changes
    /// cannot change the selection, and IPv6 router advertisements refresh route lifetimes periodically,
    /// which would only re-run the adapter evaluation for nothing.
    /// The callback runs on a Windows thread-pool thread and may fire in bursts. Dispose the result to
    /// stop notifications. Returns null if the subscription could not be created.
    /// </summary>
    public static IDisposable? SubscribeRouteChanges(Action onChange)
    {
        try
        {
            var subscription = new RouteChangeSubscription(onChange);
            int result = NotifyRouteChange2(AfInet, subscription.Callback, IntPtr.Zero, false, out IntPtr handle);
            if (result != 0)
            {
                EventLogger.Error($"Failed to subscribe to route changes (error {result})");
                return null;
            }

            subscription.SetHandle(handle);
            return subscription;
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to subscribe to route changes", ex);
            return null;
        }
    }

    /// <summary>
    /// Owns the native notification handle and keeps the callback delegate alive for as long as it is
    /// registered, so the GC cannot collect it while native code can still call it.
    /// </summary>
    private sealed class RouteChangeSubscription : IDisposable
    {
        private readonly Action onChange;
        private readonly object handleLock = new();
        private IntPtr handle;
        private volatile bool disposed;

        public RouteChangeSubscription(Action onChange)
        {
            this.onChange = onChange;
            Callback = OnRouteChanged;
        }

        public RouteChangeCallback Callback { get; }

        public void SetHandle(IntPtr notificationHandle)
        {
            bool cancelNow;
            lock (handleLock)
            {
                cancelNow = disposed;
                if (!cancelNow)
                    handle = notificationHandle;
            }

            if (cancelNow)
                Cancel(notificationHandle);
        }

        public void Dispose()
        {
            IntPtr toCancel;
            lock (handleLock)
            {
                if (disposed)
                    return;

                disposed = true;
                toCancel = handle;
                handle = IntPtr.Zero;
            }

            if (toCancel != IntPtr.Zero)
                Cancel(toCancel);

            GC.KeepAlive(Callback);
        }

        private void OnRouteChanged(IntPtr callerContext, IntPtr row, int notificationType)
        {
            // Never let an exception escape into native code.
            try
            {
                if (disposed)
                    return;

                onChange();
            }
            catch (Exception ex)
            {
                try
                {
                    EventLogger.Error("Route change handler failed", ex);
                }
                catch
                {
                    // Nothing more can be done here.
                }
            }
        }

        private static void Cancel(IntPtr notificationHandle)
        {
            try
            {
                int result = CancelMibChangeNotify2(notificationHandle);
                if (result != 0)
                    EventLogger.Error($"Failed to cancel route change notifications (error {result})");
            }
            catch (Exception ex)
            {
                EventLogger.Error("Failed to cancel route change notifications", ex);
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void RouteChangeCallback(IntPtr callerContext, IntPtr row, int notificationType);

    [DllImport("iphlpapi.dll")]
    private static extern int GetBestInterface(uint dwDestAddr, out uint pdwBestIfIndex);

    [DllImport("iphlpapi.dll")]
    private static extern int NotifyRouteChange2(
        ushort family,
        RouteChangeCallback callback,
        IntPtr callerContext,
        [MarshalAs(UnmanagedType.U1)] bool initialNotification,
        out IntPtr notificationHandle);

    [DllImport("iphlpapi.dll")]
    private static extern int CancelMibChangeNotify2(IntPtr notificationHandle);
}
