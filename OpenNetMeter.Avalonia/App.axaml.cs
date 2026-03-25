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
            IMiniWidgetService miniWidgetService = new PlaceholderMiniWidgetService();

            desktop.Exit += (_, _) =>
            {
                EventLogger.Info("Application exiting");
                miniWidgetService.Dispose();
                SettingsManager.Save();
            };

            var windowService = new AvaloniaWindowService();
            IExternalLinkService externalLinkService = new ExternalLinkService();
            var miniWidgetViewModel = new MiniWidgetViewModel();
            INetworkCaptureService networkCaptureService = OperatingSystem.IsWindows()
                ? new WindowsNetworkCaptureService()
                : new PlaceholderNetworkCaptureService();
            IProcessIconService processIconService = OperatingSystem.IsWindows()
                ? new WindowsProcessIconService()
                : new PlaceholderProcessIconService();

            miniWidgetService = OperatingSystem.IsWindows()
                ? new WindowsMiniWidgetService(miniWidgetViewModel)
                : new PlaceholderMiniWidgetService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(windowService, networkCaptureService, processIconService, externalLinkService)
            };

            if (OperatingSystem.IsWindows())
                miniWidgetService.Show();
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
