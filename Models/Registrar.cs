using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenNetMeter.Models
{
    public class Registrar
    {
        private IntPtr hLib;
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool FreeLibrary(IntPtr hModule);
        internal delegate int PointerToMethodInvoker();
        public Registrar(string filePath)
        {
            hLib = LoadLibrary(filePath);
            if (IntPtr.Zero == hLib)
            {
                int errno = Marshal.GetLastWin32Error();
                throw new Win32Exception(errno, "Failed to load library.");
            }
        }
        public void RegisterComDLL()
        {
            CallPointerMethod("DllRegisterServer");
        }
        public void UnRegisterComDLL()
        {
            CallPointerMethod("DllUnregisterServer");
        }

        internal delegate int PointerToMethodInvokerVoid();
        public void Test()
        {
            IntPtr dllEntryPoint = GetProcAddress(hLib, "TestV1");
            if (IntPtr.Zero == dllEntryPoint)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            PointerToMethodInvokerVoid drs =
                   (PointerToMethodInvokerVoid)Marshal.GetDelegateForFunctionPointer(dllEntryPoint,
                               typeof(PointerToMethodInvokerVoid));
            Debug.WriteLine("eyy: " + drs());
        }

        private void CallPointerMethod(string methodName)
        {
            IntPtr dllEntryPoint = GetProcAddress(hLib, methodName);
            if (IntPtr.Zero == dllEntryPoint)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            PointerToMethodInvoker drs =
                   (PointerToMethodInvoker)Marshal.GetDelegateForFunctionPointer(dllEntryPoint,
                               typeof(PointerToMethodInvoker));
            drs();
        }

        public void FreeLib()
        {
            if (IntPtr.Zero != hLib)
            {
                FreeLibrary(hLib);
                hLib = IntPtr.Zero;
            }
        }
    }
}
