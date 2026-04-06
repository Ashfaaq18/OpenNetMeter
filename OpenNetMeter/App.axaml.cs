using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;
using OpenNetMeter.Views;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;

namespace OpenNetMeter;

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
            desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            bool startMinimized = HasStartMinimizedArgument();

            IMiniWidgetService miniWidgetService = new PlaceholderMiniWidgetService();
            ITrayService trayService = new PlaceholderTrayService();
            ITrayNotificationService trayNotificationService = new PlaceholderTrayNotificationService();

            desktop.Exit += (_, _) =>
            {
                EventLogger.Info("Application exiting");
                trayService.Dispose();
                trayNotificationService.Dispose();
                miniWidgetService.Dispose();
                SettingsManager.Save();
            };

            var windowService = new AvaloniaWindowService();
            IExternalLinkService externalLinkService = new ExternalLinkService();
            IThemeService themeService = new AvaloniaThemeService(this);
            var miniWidgetViewModel = new MiniWidgetViewModel();
            IStartupRegistrationService startupRegistrationService = OperatingSystem.IsWindows()
                ? new WindowsStartupRegistrationService()
                : new PlaceholderStartupRegistrationService();
            INetworkCaptureService networkCaptureService = OperatingSystem.IsWindows()
                ? new WindowsNetworkCaptureService()
                : new PlaceholderNetworkCaptureService();
            IProcessIconService processIconService = OperatingSystem.IsWindows()
                ? new WindowsProcessIconService()
                : new PlaceholderProcessIconService();
            var mainWindow = new MainWindow();

            miniWidgetService = OperatingSystem.IsWindows()
                ? new WindowsMiniWidgetService(miniWidgetViewModel, mainWindow)
                : new PlaceholderMiniWidgetService();

            trayNotificationService = OperatingSystem.IsWindows()
                ? new WindowsTrayNotificationService()
                : new PlaceholderTrayNotificationService();

            mainWindow.InitializeWindowState(miniWidgetService, trayNotificationService);
            mainWindow.DataContext = new MainWindowViewModel(windowService, networkCaptureService, processIconService, externalLinkService, miniWidgetViewModel, miniWidgetService, startupRegistrationService, themeService);
            desktop.MainWindow = mainWindow;

            trayService = OperatingSystem.IsWindows()
                ? new WindowsTrayService(this, desktop, mainWindow, miniWidgetService)
                : new PlaceholderTrayService();

            if (startMinimized)
                mainWindow.Hide();

            if (OperatingSystem.IsWindows() && SettingsManager.Current.MiniWidgetVisibility)
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

    private static bool HasStartMinimizedArgument()
    {
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, "/StartMinimized", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
