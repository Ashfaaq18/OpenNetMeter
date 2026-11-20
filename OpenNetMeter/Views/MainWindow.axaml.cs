using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer resizeTimer;
    private readonly DispatcherTimer relocationTimer;
    private IMiniWidgetService? miniWidgetService;
    private ITrayNotificationService? trayNotificationService;
    private bool allowClose;
    // True from the first show to the end of a startup pre-warm (see StartPreWarm): the window is shown invisibly, so
    // the handlers that react to the user opening or moving it (position init, geometry saves, widget reset,
    // the tray hint, activity reports) must not run for it.
    private bool isWarmingUp;
    // The window has been shown to the user (not just pre-warmed).
    private bool hasBeenShown;
    // Opened was raised during the pre-warm and ignored. Normally the user's first show raises it again; if it
    // doesn't (the event might be once per window), ShowForUser does what it would have done.
    private bool openedWhilePreWarming;
    private MainWindowPreWarm? preWarm;

    public MainWindow()
    {
        InitializeComponent();

        resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        relocationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        resizeTimer.Tick += ResizeTimer_Tick;
        relocationTimer.Tick += RelocationTimer_Tick;

        Opened += MainWindow_Opened;
        Closing += MainWindow_Closing;
        PositionChanged += MainWindow_PositionChanged;
        SizeChanged += MainWindow_SizeChanged;

        // The rest of the window is disabled while a dialog is open, so move focus into the dialog.
        // "No" is the safe default for confirmations.
        FocusWhenShown(ConfirmationOverlay, ConfirmNoButton);
        FocusWhenShown(AboutOverlay, AboutCloseButton);
        FocusWhenShown(RecordingResultsOverlay, RecordingResultsCloseButton);
    }

    private static void FocusWhenShown(Control overlay, Control target)
    {
        overlay.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && overlay.IsVisible)
                Dispatcher.UIThread.Post(() => target.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        };
    }

    public void InitializeWindowState(IMiniWidgetService miniWidgetService, ITrayNotificationService trayNotificationService)
    {
        this.miniWidgetService = miniWidgetService;
        this.trayNotificationService = trayNotificationService;

        if (SettingsManager.Current.MainWindowPositionInitialized)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = Math.Max(MinWidth, SettingsManager.Current.WinWidth);
            Height = Math.Max(MinHeight, SettingsManager.Current.WinHeight);
            Position = new PixelPoint(SettingsManager.Current.WinPosX, SettingsManager.Current.WinPosY);
            return;
        }

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <summary>
    /// For a /StartMinimized launch, pays the window's first-show cost while nobody is looking: shows it invisibly for
    /// a couple of frames and hides it again, so the user's first open is as quick as the later ones. Does nothing for a
    /// normal launch, or if the window has been opened already. Call once, on the UI thread, some time after startup.
    /// </summary>
    public void StartPreWarm(bool startMinimized)
    {
        preWarm ??= new MainWindowPreWarm(new PreWarmHost(this));
        preWarm.TryStart(startMinimized);
    }

    public void OpenFromTray()
    {
        // Opened while the pre-warm runs: it finishes first (a frame or two), then this opens the window normally.
        if (preWarm is { IsWarmingUp: true })
        {
            preWarm.OpenForUser();
            return;
        }

        ShowForUser();
    }

    private void ShowForUser()
    {
        if (!IsVisible)
            Show();

        // Show raises Opened synchronously, which clears this; still set means it didn't.
        if (openedWhilePreWarming)
            HandleOpenedByUser();

        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }

    public void PrepareForExit()
    {
        allowClose = true;
    }

    public void ResetWindowPositions()
    {
        if (preWarm is { IsWarmingUp: true })
        {
            preWarm.RunWhenIdle(ResetWindowPositions);
            return;
        }

        CenterOnPrimaryScreen();
        SaveWindowGeometry();
        miniWidgetService?.ResetPosition(this);
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void ResizeTop_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.North, e);
    private void ResizeBottom_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.South, e);
    private void ResizeLeft_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.West, e);
    private void ResizeRight_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.East, e);
    private void ResizeTopLeft_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.NorthWest, e);
    private void ResizeTopRight_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.NorthEast, e);
    private void ResizeBottomLeft_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.SouthWest, e);
    private void ResizeBottomRight_PointerPressed(object? sender, PointerPressedEventArgs e) => TryBeginResize(WindowEdge.SouthEast, e);

    private void TryBeginResize(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
            BeginResizeDrag(edge, e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty || change.Property == WindowStateProperty)
            ReportActivity();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ReportActivity();
    }

    /// <summary>
    /// Tells the view model whether anything of the window can be seen (shown and not minimized), so it can hold back
    /// the notifications nothing would display.
    /// </summary>
    private void ReportActivity()
    {
        if (isWarmingUp)
            return;

        if (DataContext is MainWindowViewModel vm)
            vm.SetWindowActive(IsVisible && WindowState != WindowState.Minimized);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        resizeTimer.Stop();
        relocationTimer.Stop();

        if (DataContext is MainWindowViewModel vm)
            vm.Dispose();

        base.OnClosed(e);
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (allowClose)
            return;

        e.Cancel = true;
        if (!isWarmingUp)
            trayNotificationService?.ShowMinimizedToTrayOnce(this);
        Hide();
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (isWarmingUp)
        {
            openedWhilePreWarming = true;
            return;
        }

        HandleOpenedByUser();
    }

    private void HandleOpenedByUser()
    {
        openedWhilePreWarming = false;
        hasBeenShown = true;

        if (!SettingsManager.Current.MainWindowPositionInitialized)
        {
            SaveWindowGeometry();
            miniWidgetService?.ResetPosition(this);
            return;
        }

        if (!IsWindowInBounds(this))
        {
            CenterOnPrimaryScreen();
            SaveWindowGeometry();
        }

        miniWidgetService?.EnsurePositionOnScreen(this);
    }

    private void MainWindow_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (isWarmingUp || WindowState != WindowState.Normal)
            return;

        RestartTimer(relocationTimer);
    }

    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (isWarmingUp || WindowState != WindowState.Normal)
            return;

        RestartTimer(resizeTimer);
    }

    private void ResizeTimer_Tick(object? sender, EventArgs e)
    {
        resizeTimer.Stop();
        SaveWindowGeometry();
    }

    private void RelocationTimer_Tick(object? sender, EventArgs e)
    {
        relocationTimer.Stop();
        SaveWindowGeometry();
    }

    private void SaveWindowGeometry()
    {
        if (isWarmingUp || WindowState != WindowState.Normal)
            return;

        // The values are taken here, on the UI thread; the file is read, merged and written on a background task, so
        // dragging or resizing the window never waits on the disk.
        SettingsManager.Current.WinPosX = Position.X;
        SettingsManager.Current.WinPosY = Position.Y;
        SettingsManager.Current.WinWidth = Math.Max((int)MinWidth, (int)Math.Round(Bounds.Width > 0 ? Bounds.Width : Width));
        SettingsManager.Current.WinHeight = Math.Max((int)MinHeight, (int)Math.Round(Bounds.Height > 0 ? Bounds.Height : Height));
        SettingsManager.Current.MainWindowPositionInitialized = true;
        SettingsManager.SaveInBackground();
    }

    private void CenterOnPrimaryScreen()
    {
        var screen = Screens?.Primary ?? Screens?.All[0];
        if (screen == null)
            return;

        var width = Math.Max((int)MinWidth, (int)Math.Round(Bounds.Width > 0 ? Bounds.Width : Width));
        var height = Math.Max((int)MinHeight, (int)Math.Round(Bounds.Height > 0 ? Bounds.Height : Height));
        var area = screen.WorkingArea;

        Position = new PixelPoint(
            area.X + Math.Max(0, (area.Width - width) / 2),
            area.Y + Math.Max(0, (area.Height - height) / 2));
    }

    private static void RestartTimer(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private static bool IsWindowInBounds(Window target)
    {
        var screens = target.Screens?.All;
        if (screens is null || screens.Count == 0)
            return true;

        var width = Math.Max(1, (int)Math.Round(target.Bounds.Width > 0 ? target.Bounds.Width : target.Width));
        var height = Math.Max(1, (int)Math.Round(target.Bounds.Height > 0 ? target.Bounds.Height : target.Height));
        const int margin = 32;

        foreach (var screen in screens)
        {
            var area = screen.WorkingArea;
            var areaRight = area.X + area.Width;
            var areaBottom = area.Y + area.Height;
            var targetRight = target.Position.X + width;
            var targetBottom = target.Position.Y + height;

            if (area.X < targetRight - margin &&
                areaRight > target.Position.X + margin &&
                area.Y < targetBottom - margin &&
                areaBottom > target.Position.Y + margin)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Carries out the pre-warm steps on this window.</summary>
    private sealed class PreWarmHost(MainWindow window) : IPreWarmHost
    {
        private bool savedShowActivated;
        private bool savedShowInTaskbar;
        private WindowStartupLocation savedStartupLocation;
        private PixelPoint savedPosition;

        public bool HasBeenShown => window.hasBeenShown;

        public bool IsVisible => window.IsVisible;

        public void BeginWarmUp()
        {
            window.isWarmingUp = true;
            savedShowActivated = window.ShowActivated;
            savedShowInTaskbar = window.ShowInTaskbar;
            // No focus steal and no taskbar button flashing up for a window the user can't see.
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
        }

        public bool TryCloak() =>
            window.TryGetPlatformHandle() is { } handle && WindowsDwmCloak.TrySetCloaked(handle.Handle, true);

        public bool TryMoveOffScreen()
        {
            var screens = window.Screens?.All;
            if (screens is null || screens.Count == 0)
                return false;

            var right = int.MinValue;
            var bottom = int.MinValue;
            foreach (var screen in screens)
            {
                right = Math.Max(right, screen.Bounds.Right);
                bottom = Math.Max(bottom, screen.Bounds.Bottom);
            }

            savedStartupLocation = window.WindowStartupLocation;
            savedPosition = window.Position;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(right + 200, bottom + 200);
            return true;
        }

        public void Show() => window.Show();

        public void WaitForFrames(int count, Action then)
        {
            void Step(int remaining)
            {
                if (remaining <= 0)
                {
                    // Not from inside the frame callback: hiding the window stops its rendering, which shouldn't
                    // happen in the middle of a render pass.
                    Dispatcher.UIThread.Post(then, DispatcherPriority.Background);
                    return;
                }

                window.RequestAnimationFrame(_ => Step(remaining - 1));
            }

            Step(count);
        }

        public void ScheduleTimeout(TimeSpan delay, Action then) => DispatcherTimer.RunOnce(then, delay);

        public void Hide()
        {
            if (window.IsVisible)
                window.Hide();
        }

        public void Uncloak()
        {
            // A window that stayed cloaked would never be seen again, so check it took and try a couple more times.
            var hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                WindowsDwmCloak.TrySetCloaked(hwnd, false);
                if (!WindowsDwmCloak.IsCloakedByApp(hwnd))
                    return;
            }

            EventLogger.Error("The pre-warmed main window is still cloaked after uncloaking it");
        }

        public void RestorePosition()
        {
            window.WindowStartupLocation = savedStartupLocation;
            if (SettingsManager.Current.MainWindowPositionInitialized)
                window.Position = savedPosition;
            else
                window.CenterOnPrimaryScreen();
        }

        public void EndWarmUp()
        {
            window.ShowActivated = savedShowActivated;
            window.ShowInTaskbar = savedShowInTaskbar;
            window.isWarmingUp = false;
            window.ReportActivity();
        }

        public void Completed() => StartupTiming.LogMainWindowPreWarmed();

        public void ShowNormally() => window.ShowForUser();

        public void LogFailure(string message, Exception ex) => EventLogger.Error(message, ex);
    }
}

