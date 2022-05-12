using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace OpenNetMeter.Models
{
    public static class NativeMethods
    {
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        
        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool BringWindowToTop(IntPtr hWnd);

        public static IntPtr FindWindowByClassName(IntPtr hwndParent, string className)
        {
            return FindWindowEx(hwndParent, IntPtr.Zero, className, null);
        }
    }
}
