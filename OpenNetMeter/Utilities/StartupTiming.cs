using System;
using System.Diagnostics;
using System.Threading;

namespace OpenNetMeter.Utilities;

/// <summary>
/// Logs how long startup milestones took, measured from the process start time, so a slow cold start
/// can be diagnosed from the event log. Each milestone is logged at most once per run.
/// </summary>
internal static class StartupTiming
{
    private static int widgetShownLogged;
    private static int captureStartedLogged;

    public static void LogWidgetShown() =>
        LogOnce(ref widgetShownLogged, "Startup: widget shown");

    public static void LogCaptureStarted() =>
        LogOnce(ref captureStartedLogged, "Startup: capture started");

    private static void LogOnce(ref int flag, string milestone)
    {
        if (Interlocked.Exchange(ref flag, 1) != 0)
            return;

        try
        {
            using var process = Process.GetCurrentProcess();
            var elapsed = DateTime.Now - process.StartTime;
            EventLogger.Info($"{milestone} {(long)elapsed.TotalMilliseconds} ms after process start");
        }
        catch (Exception ex)
        {
            // Timing is diagnostic only; never let it affect startup.
            Debug.WriteLine($"Startup timing unavailable: {ex.Message}");
        }
    }
}
