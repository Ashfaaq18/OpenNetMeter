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
    private static int captureBegunLogged;
    private static int etwEnabledLogged;
    private static int widgetShownLogged;
    private static int mainWindowBuiltLogged;
    private static int captureStartedLogged;

    /// <summary>The background capture start has begun, before any window or view model exists.</summary>
    public static void LogCaptureBegun() =>
        LogOnce(ref captureBegunLogged, "Startup: capture begun");

    /// <summary>The ETW kernel provider is enabled, so packets are being traced from here on.</summary>
    public static void LogEtwEnabled() =>
        LogOnce(ref etwEnabledLogged, "Startup: ETW enabled");

    public static void LogWidgetShown() =>
        LogOnce(ref widgetShownLogged, "Startup: widget shown");

    /// <summary>The deferred step that builds the main window, view bindings and tray icon has finished.</summary>
    public static void LogMainWindowBuilt() =>
        LogOnce(ref mainWindowBuiltLogged, "Startup: main window built");

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
