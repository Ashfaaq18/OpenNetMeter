using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer resizeTimer;
    private readonly DispatcherTimer relocationTimer;
    private IMiniWidgetService? miniWidgetService;
    private bool allowClose;

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
    }

    public void InitializeWindowState(IMiniWidgetService miniWidgetService)
    {
        this.miniWidgetService = miniWidgetService;

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

    public void OpenFromTray()
    {
        if (!IsVisible)
            Show();

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
        Hide();
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
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
        if (WindowState != WindowState.Normal)
            return;

        RestartTimer(relocationTimer);
    }

    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (WindowState != WindowState.Normal)
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
        if (WindowState != WindowState.Normal)
            return;

        SettingsManager.Current.WinPosX = Position.X;
        SettingsManager.Current.WinPosY = Position.Y;
        SettingsManager.Current.WinWidth = Math.Max((int)MinWidth, (int)Math.Round(Bounds.Width > 0 ? Bounds.Width : Width));
        SettingsManager.Current.WinHeight = Math.Max((int)MinHeight, (int)Math.Round(Bounds.Height > 0 ? Bounds.Height : Height));
        SettingsManager.Current.MainWindowPositionInitialized = true;
        SettingsManager.Save();
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
}

