using System;
using Avalonia;
using Avalonia.Controls;
using OpenNetMeter.Avalonia.ViewModels;
using OpenNetMeter.Avalonia.Views;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Avalonia.Services;

public sealed class WindowsMiniWidgetService : IMiniWidgetService
{
    private readonly Window mainWindow;
    private readonly MiniWidgetViewModel viewModel;
    private readonly MiniWidgetWindow window;
    private readonly WindowsWidgetZOrderHelper zOrderHelper;
    private bool positionTrackingEnabled;
    private bool restoringPosition;

    public event Action<bool>? VisibilityChanged;

    public WindowsMiniWidgetService(MiniWidgetViewModel viewModel, Window mainWindow)
    {
        this.mainWindow = mainWindow;
        this.viewModel = viewModel;
        window = new MiniWidgetWindow
        {
            DataContext = viewModel
        };
        zOrderHelper = new WindowsWidgetZOrderHelper(window);

        viewModel.SetActions(OpenMainWindow, Hide);
        window.Opened += Window_Opened;
        window.PositionChanged += Window_PositionChanged;
    }

    public void Show()
    {
        try
        {
            if (!window.IsVisible)
                window.Show();
            else
                window.Activate();

            zOrderHelper.Start();
            VisibilityChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to show mini widget window", ex);
        }
    }

    public void Hide()
    {
        try
        {
            if (window.IsVisible)
                window.Hide();

            zOrderHelper.Stop();
            VisibilityChanged?.Invoke(false);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to hide mini widget window", ex);
        }
    }

    public void RefreshAppearance(bool darkMode, int transparency)
    {
        viewModel.RefreshBackground(darkMode, transparency);
    }

    public void ResetPosition(Window mainWindow)
    {
        try
        {
            restoringPosition = true;

            var mainWidth = Math.Max(1, (int)Math.Round(mainWindow.Bounds.Width));
            var mainHeight = Math.Max(1, (int)Math.Round(mainWindow.Bounds.Height));
            var widgetWidth = Math.Max(1, (int)Math.Round(window.Bounds.Width > 0 ? window.Bounds.Width : window.Width));
            var widgetHeight = Math.Max(1, (int)Math.Round(window.Bounds.Height > 0 ? window.Bounds.Height : window.Height));

            var x = mainWindow.Position.X + (mainWidth / 2) - (widgetWidth / 2);
            var y = mainWindow.Position.Y + (mainHeight / 2) - (widgetHeight / 2);

            window.Position = new PixelPoint(x, y);
            SaveWindowPosition();
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to reset mini widget window position", ex);
        }
        finally
        {
            restoringPosition = false;
        }
    }

    public void EnsurePositionOnScreen(Window mainWindow)
    {
        try
        {
            if (!SettingsManager.Current.MiniWidgetPositionInitialized || !IsWindowInBounds(window))
                ResetPosition(mainWindow);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to validate mini widget window position", ex);
        }
    }

    public void Dispose()
    {
        try
        {
            zOrderHelper.Dispose();
            window.Close();
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to dispose mini widget window", ex);
        }
    }

    private void OpenMainWindow()
    {
        try
        {
            if (!mainWindow.IsVisible)
                mainWindow.Show();

            if (mainWindow.WindowState == WindowState.Minimized)
                mainWindow.WindowState = WindowState.Normal;

            mainWindow.Activate();
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to open main window from mini widget", ex);
        }
    }

    private void Window_Opened(object? sender, EventArgs e)
    {
        try
        {
            if (SettingsManager.Current.MiniWidgetPositionInitialized)
            {
                restoringPosition = true;
                window.Position = new PixelPoint(SettingsManager.Current.MiniWidgetPosX, SettingsManager.Current.MiniWidgetPosY);
                restoringPosition = false;
            }
            else
            {
                SaveWindowPosition();
            }

            positionTrackingEnabled = true;
        }
        catch (Exception ex)
        {
            restoringPosition = false;
            EventLogger.Error("Failed to restore mini widget window position", ex);
        }
    }

    private void Window_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (restoringPosition || !positionTrackingEnabled)
            return;

        try
        {
            SaveWindowPosition();
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to save mini widget window position", ex);
        }
    }

    private void SaveWindowPosition()
    {
        SettingsManager.Current.MiniWidgetPosX = window.Position.X;
        SettingsManager.Current.MiniWidgetPosY = window.Position.Y;
        SettingsManager.Current.MiniWidgetPositionInitialized = true;
        SettingsManager.Save();
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
            // Use full screen bounds here, not WorkingArea, so a widget intentionally
            // positioned over the taskbar is still considered a valid persisted position.
            var area = screen.Bounds;
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
