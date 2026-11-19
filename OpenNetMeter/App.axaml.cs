using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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

            // Startup order is chosen to get the mini widget on screen first: everything up to widget.Show() below is
            // what the widget waits for, and the main window (three tabs, three overlays, hundreds of bindings) is
            // built afterwards in a deferred step.

            // The saved theme goes on before any window exists, so no resource is resolved against the wrong
            // variant and then re-resolved when the theme changes.
            IThemeService themeService = new AvaloniaThemeService(this);
            themeService.ApplyDarkMode(SettingsManager.Current.DarkMode);

            // Capture is the slowest part of startup (adapters, WLAN event log, ETW session, database), so it begins
            // before any window or view model is built and overlaps with all of it.
            INetworkCaptureService networkCaptureService = OperatingSystem.IsWindows()
                ? new WindowsNetworkCaptureService()
                : new PlaceholderNetworkCaptureService();
            Task<bool> captureStart = StartCaptureInBackground(networkCaptureService);

            IMiniWidgetService miniWidgetService = new PlaceholderMiniWidgetService();
            ITrayService trayService = new PlaceholderTrayService();
            ITrayNotificationService trayNotificationService = OperatingSystem.IsWindows()
                ? new WindowsTrayNotificationService()
                : new PlaceholderTrayNotificationService();

            desktop.Exit += (_, _) =>
            {
                EventLogger.Info("Application exiting");
                trayService.Dispose();
                trayNotificationService.Dispose();
                miniWidgetService.Dispose();
                SettingsManager.Save();
                // Normally the main window's view model stops capture when the window closes, but the window may
                // not exist yet if the app exits during startup. Disposing twice is harmless.
                networkCaptureService.Dispose();
                // Event Log entries are written on a background thread; give the last ones a moment to land.
                EventLogger.Flush(TimeSpan.FromSeconds(1));
            };

            var windowService = new AvaloniaWindowService();
            IExternalLinkService externalLinkService = new ExternalLinkService();
            IFileSaveDialogService fileSaveDialogService = new AvaloniaFileSaveDialogService();
            IFileRevealService fileRevealService = new FileRevealService();
            var miniWidgetViewModel = new MiniWidgetViewModel();
            IStartupRegistrationService startupRegistrationService = OperatingSystem.IsWindows()
                ? new WindowsStartupRegistrationService()
                : new PlaceholderStartupRegistrationService();
            IProcessIconService processIconService = OperatingSystem.IsWindows()
                ? new WindowsProcessIconService()
                : new PlaceholderProcessIconService();

            // The window is built on first use rather than up front. The deferred step below normally gets there
            // first, but the widget's Open command, the tray and a second launch's activation can all ask for it
            // sooner. Only ever called on the UI thread, and never before startup completes: the desktop lifetime
            // shows a MainWindow that is assigned by then, which a /StartMinimized launch must not do.
            MainWindowViewModel? mainViewModel = null;
            MainWindow? mainWindow = null;
            MainWindow EnsureMainWindow()
            {
                if (mainWindow != null)
                    return mainWindow;

                var window = new MainWindow();
                window.InitializeWindowState(miniWidgetService, trayNotificationService);
                window.DataContext = mainViewModel;
                // The view model's window commands (minimize, close) find the window through the lifetime.
                desktop.MainWindow = window;
                mainWindow = window;
                return window;
            }

            miniWidgetService = OperatingSystem.IsWindows()
                ? new WindowsMiniWidgetService(miniWidgetViewModel, EnsureMainWindow)
                : new PlaceholderMiniWidgetService();

            // Built before the widget is shown (it has no synchronous I/O) because the widget's text comes from it.
            var viewModel = new MainWindowViewModel(windowService, networkCaptureService, processIconService, externalLinkService, miniWidgetViewModel, miniWidgetService, startupRegistrationService, themeService, fileSaveDialogService, fileRevealService);
            mainViewModel = viewModel;

            // Work that must not compete with the capture start runs once it has returned (success or not).
            _ = captureStart.ContinueWith(
                started =>
                {
                    if (started.Result)
                        Dispatcher.UIThread.Post(viewModel.OnCaptureStartCompleted);
                },
                TaskScheduler.Default);

            if (OperatingSystem.IsWindows() && SettingsManager.Current.MiniWidgetVisibility)
            {
                miniWidgetService.Show();
                // Loaded priority runs after the widget's first layout and render, so this is when it is on screen.
                Dispatcher.UIThread.Post(StartupTiming.LogWidgetShown, DispatcherPriority.Loaded);
            }

            // Background priority runs after the widget's first layout and render (and after the Default-priority
            // network updates queued above), so none of this delays the widget.
            Dispatcher.UIThread.Post(() =>
            {
                var window = EnsureMainWindow();

                // A /StartMinimized launch stays in the tray; the tray icon (or the widget) shows the window.
                // Shown before the tray icon is added so a slow Explorer can't delay the window.
                if (!startMinimized && !window.IsVisible)
                    window.Show();

                // The tray icon is added after the window, not earlier: it is a synchronous call into Explorer and
                // needs the window.
                trayService = OperatingSystem.IsWindows()
                    ? new WindowsTrayService(this, desktop, window, miniWidgetService)
                    : new PlaceholderTrayService();

                // A second launch (shortcut, Start menu) exits and asks this copy to show its window instead. One that
                // arrived before this point is still pending on the event and is delivered as soon as this registers.
                Program.InstanceGuard?.ListenForActivation(() => Dispatcher.UIThread.Post(() => EnsureMainWindow().OpenFromTray()));

                StartupTiming.LogMainWindowBuilt();
            }, DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Start() enumerates adapters, reads the WLAN event log, starts the ETW session and opens the database.
    /// On the UI thread that kept the widget from appearing for seconds, so it runs in the background. The
    /// capture service serialises Start/Stop and raises its events from worker threads anyway, and every
    /// subscriber (MainWindowViewModel, SummaryViewModel.OnTrafficObserved) is safe to call off the UI thread.
    /// Subscribers that attach after NetworkChanged has already fired catch up through CurrentSnapshot.
    /// </summary>
    /// <returns>False if the service was disposed before the start ran (the app is exiting); true otherwise,
    /// including when the start failed, which is logged.</returns>
    private static Task<bool> StartCaptureInBackground(INetworkCaptureService networkCaptureService)
    {
        return Task.Run(() =>
        {
            StartupTiming.LogCaptureBegun();
            try
            {
                networkCaptureService.Start();
                StartupTiming.LogCaptureStarted();
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (Exception ex)
            {
                EventLogger.Error("Failed to start network capture", ex);
            }

            return true;
        });
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

            // The process is about to terminate; don't lose the entry that explains why.
            EventLogger.Flush(TimeSpan.FromSeconds(1));
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
