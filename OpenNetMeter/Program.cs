using System;
using System.Linq;
using Avalonia;
using OpenNetMeter.Services;

namespace OpenNetMeter;

internal sealed class Program
{
    /// <summary>Held for the process lifetime; null only before Main runs.</summary>
    internal static SingleInstanceGuard? InstanceGuard { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // A launch at sign-in (/StartMinimized) shouldn't pop up an already running copy;
        // any other launch (shortcut, Start menu) brings it to the front.
        bool startMinimized = args.Any(a => string.Equals(a, "/StartMinimized", StringComparison.OrdinalIgnoreCase));
        InstanceGuard = SingleInstanceGuard.TryAcquire(activateExisting: !startMinimized);
        if (InstanceGuard == null)
            return;

        using (InstanceGuard)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
