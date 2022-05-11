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
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        [DllImport("User32.dll", SetLastError = true)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("User32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

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
