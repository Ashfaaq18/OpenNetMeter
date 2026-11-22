using System;
using System.Runtime.InteropServices;

namespace OpenNetMeter.Services;

internal static class AppPackage
{
    private const int AppModelErrorNoPackage = 15700;

    /// <summary>True when running as an MSIX package (Microsoft Store).</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            uint length = 0;
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, char[]? packageFullName);
}
