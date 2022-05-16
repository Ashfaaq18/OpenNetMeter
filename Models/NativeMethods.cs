using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace OpenNetMeter.Models
{
    public static class NativeMethods
    {
        
        internal static IntPtr GetWindowByClassName(IntPtr parentHandle, string className) => FindWindowEx(parentHandle, IntPtr.Zero, className, string.Empty);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        public enum GW : uint
        {
            HWNDFIRST = 0,
            HWNDLAST = 1,
            HWNDNEXT = 2,
            HWNDPREV = 3,
            OWNER = 4,
            CHILD = 5,
            ENABLEDPOPUP = 6
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

        public enum SWP
        {
            ASYNCWINDOWPOS = 0x4000,
            NOACTIVATE = 0x0010,
            NOMOVE = 0x0002,
            NOSIZE = 0x0001,
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        internal static extern IntPtr SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int wFlags);
    }
}
