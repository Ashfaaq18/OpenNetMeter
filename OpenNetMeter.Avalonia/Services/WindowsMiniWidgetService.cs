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
    private readonly MiniWidgetWindow window;
    private bool restoringPosition;

    public WindowsMiniWidgetService(MiniWidgetViewModel viewModel, Window mainWindow)
    {
        this.mainWindow = mainWindow;
        window = new MiniWidgetWindow
        {
            DataContext = viewModel
        };

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
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to hide mini widget window", ex);
        }
    }

    public void Dispose()
    {
        try
        {
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
                return;
            }

            SaveWindowPosition();
        }
        catch (Exception ex)
        {
            restoringPosition = false;
            EventLogger.Error("Failed to restore mini widget window position", ex);
        }
    }

    private void Window_PositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (restoringPosition)
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
}
