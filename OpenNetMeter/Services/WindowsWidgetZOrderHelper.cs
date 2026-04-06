using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Services;

internal sealed class WindowsWidgetZOrderHelper : IDisposable
{
    private static readonly IntPtr HwndTopMost = new(-1);
    private const int GwlpHwndParent = -8;
    private const string ShellTrayWindowClass = "Shell_TrayWnd";

    private readonly Window window;
    private readonly DispatcherTimer timer;

    public WindowsWidgetZOrderHelper(Window window)
    {
        this.window = window;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += Timer_Tick;
    }

    public void Start()
    {
        timer.Start();
    }

    public void Stop()
    {
        timer.Stop();
    }

    public void Dispose()
    {
        timer.Stop();
        timer.Tick -= Timer_Tick;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var windowHandle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (windowHandle == IntPtr.Zero)
                return;

            var shellTray = FindWindowEx(IntPtr.Zero, IntPtr.Zero, ShellTrayWindowClass, string.Empty);
            if (shellTray == IntPtr.Zero)
                return;

            var owner = GetWindowLongPtr(windowHandle, GwlpHwndParent);
            if (owner != shellTray)
                SetWindowLongPtr(windowHandle, GwlpHwndParent, shellTray);

            for (var current = windowHandle; current != IntPtr.Zero; current = GetWindow(current, 3))
            {
                if (current != shellTray)
                    continue;

                SetWindowPos(
                    windowHandle,
                    HwndTopMost,
                    0,
                    0,
                    0,
                    0,
                    0x4000 | 0x0010 | 0x0002 | 0x0001);
                break;
            }
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to maintain mini widget z-order", ex);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int wFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }
}

