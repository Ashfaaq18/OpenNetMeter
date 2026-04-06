using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Services;

[SupportedOSPlatform("windows")]
public sealed class WindowsTrayNotificationService : ITrayNotificationService
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;

    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint NIF_INFO = 0x00000010;

    private const uint NIIF_NONE = 0x00000000;
    private const uint BalloonIconId = 0x4F4E4D;

    private bool hasShownMinimizedNotification;
    private Icon? balloonIcon;

    public void ShowMinimizedToTrayOnce(Window mainWindow)
    {
        if (hasShownMinimizedNotification)
            return;

        try
        {
            var handle = mainWindow.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle == IntPtr.Zero)
                return;

            balloonIcon?.Dispose();
            balloonIcon = LoadNotificationIcon();

            var addData = CreateNotifyIconData(handle, balloonIcon.Handle);
            addData.uFlags = NIF_ICON | NIF_TIP;
            addData.szTip = "OpenNetMeter";

            if (!Shell_NotifyIcon(NIM_ADD, ref addData))
                return;

            var infoData = CreateNotifyIconData(handle, balloonIcon.Handle);
            infoData.uFlags = NIF_INFO;
            infoData.szInfo = "Minimized to system tray";
            infoData.szInfoTitle = string.Empty;
            infoData.dwInfoFlags = NIIF_NONE;
            infoData.uTimeoutOrVersion = 1000;

            Shell_NotifyIcon(NIM_MODIFY, ref infoData);
            hasShownMinimizedNotification = true;

            _ = RemoveTemporaryIconAsync(handle);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to show minimized-to-tray notification", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            balloonIcon?.Dispose();
            balloonIcon = null;
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to dispose tray notification icon", ex);
        }
    }

    private async Task RemoveTemporaryIconAsync(IntPtr handle)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5));

            var deleteData = CreateNotifyIconData(handle, IntPtr.Zero);
            Shell_NotifyIcon(NIM_DELETE, ref deleteData);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to remove tray notification icon", ex);
        }
    }

    private static Icon LoadNotificationIcon()
    {
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && System.IO.File.Exists(processPath))
        {
            var icon = Icon.ExtractAssociatedIcon(processPath);
            if (icon != null)
                return (Icon)icon.Clone();
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static NOTIFYICONDATA CreateNotifyIconData(IntPtr handle, IntPtr iconHandle)
    {
        return new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = handle,
            uID = BalloonIconId,
            hIcon = iconHandle
        };
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
}
