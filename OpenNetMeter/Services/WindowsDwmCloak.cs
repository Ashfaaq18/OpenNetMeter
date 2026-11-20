using System;
using System.Runtime.InteropServices;

namespace OpenNetMeter.Services;

/// <summary>
/// Cloaks a window of this process with the Desktop Window Manager: the window is shown to Windows (so it lays out,
/// renders and presents as usual) but the DWM does not compose it, so nothing appears on screen. Used to pre-warm the
/// main window invisibly. Windows only; every call is a no-op that reports failure elsewhere.
/// </summary>
internal static class WindowsDwmCloak
{
    private const int DwmwaCloak = 13;
    private const int DwmwaCloaked = 14;
    private const int DwmCloakedApp = 1;

    /// <summary>Cloaks or uncloaks the window. Returns false if the DWM refused (or this isn't Windows).</summary>
    public static bool TrySetCloaked(IntPtr hwnd, bool cloaked)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
            return false;

        try
        {
            int value = cloaked ? 1 : 0;
            return DwmSetWindowAttribute(hwnd, DwmwaCloak, ref value, sizeof(int)) == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Whether the window is currently cloaked by the application (that is, by <see cref="TrySetCloaked"/>).</summary>
    public static bool IsCloakedByApp(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
            return false;

        try
        {
            return DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int value, sizeof(int)) == 0 && (value & DwmCloakedApp) != 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int valueSize);
}
