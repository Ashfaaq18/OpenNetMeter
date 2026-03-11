using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenNetMeter.Avalonia.Services;
using OpenNetMeter.Avalonia.ViewModels;
using OpenNetMeter.Avalonia.Views;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Avalonia;

public partial class App : Application
{
    private static bool unhandledHandlersRegistered;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        RegisterUnhandledExceptionLoggingOnce();
        EventLogger.Info("Application starting");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, _) =>
            {
                EventLogger.Info("Application exiting");
                SettingsManager.Save();
            };

            var windowService = new AvaloniaWindowService();
            INetworkCaptureService networkCaptureService = OperatingSystem.IsWindows()
                ? new WindowsNetworkCaptureService()
                : new PlaceholderNetworkCaptureService();
            IProcessIconService processIconService = OperatingSystem.IsWindows()
                ? new WindowsProcessIconService()
                : new PlaceholderProcessIconService();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(windowService, networkCaptureService, processIconService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterUnhandledExceptionLoggingOnce()
    {
        if (unhandledHandlersRegistered)
            return;

        unhandledHandlersRegistered = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                EventLogger.Error("Unhandled exception", ex);
            }
            else
            {
                EventLogger.Error($"Unhandled exception object: {e.ExceptionObject}");
            }
        };
    }
}
