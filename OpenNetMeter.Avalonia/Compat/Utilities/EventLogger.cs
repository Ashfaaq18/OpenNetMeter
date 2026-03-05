using System;
using System.Diagnostics;

namespace OpenNetMeter.Utilities;

public static class EventLogger
{
    public static void Error(string message, Exception ex)
    {
        Debug.WriteLine($"{message}: {ex}");
    }

    public static void Error(string message)
    {
        Debug.WriteLine(message);
    }
}
