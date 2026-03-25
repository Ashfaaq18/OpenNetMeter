using System;
using Avalonia.Controls;
using OpenNetMeter.Avalonia.ViewModels;
using OpenNetMeter.Avalonia.Views;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Avalonia.Services;

public sealed class WindowsMiniWidgetService : IMiniWidgetService
{
    private readonly Window mainWindow;
    private readonly MiniWidgetWindow window;

    public WindowsMiniWidgetService(MiniWidgetViewModel viewModel, Window mainWindow)
    {
        this.mainWindow = mainWindow;
        window = new MiniWidgetWindow
        {
            DataContext = viewModel
        };

        viewModel.SetActions(OpenMainWindow, Hide);
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
}
