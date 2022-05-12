using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenNetMeter.Models
{
    public static class NativeMethods
    {
        enum GetClipBoxReturn : int
        {
            Error = 0,
            NullRegion = 1,
            SimpleRegion = 2,
            ComplexRegion = 3
        }
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

        [DllImport("user32.dll")]
        internal static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool BringWindowToTop(IntPtr hWnd);

        public static IntPtr FindWindowByClassName(IntPtr hwndParent, string className)
        {
            return FindWindowEx(hwndParent, IntPtr.Zero, className, null);
        }

        public static Rectangle GetWindowRectangle(IntPtr windowHandle)
        {
            RECT rect;
            GetWindowRect(windowHandle, out rect);
            return rect.ToRectangle();
        }
    }
}
