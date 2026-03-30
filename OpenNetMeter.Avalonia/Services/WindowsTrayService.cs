using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using OpenNetMeter.Avalonia.Views;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Avalonia.Services;

public sealed class WindowsTrayService : ITrayService
{
    private readonly TrayIcon trayIcon;

    public WindowsTrayService(
        Application application,
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow,
        IMiniWidgetService miniWidgetService)
    {
        var menu = new NativeMenu();

        var resetPositionsItem = new NativeMenuItem("Reset all window positions");
        resetPositionsItem.Click += (_, _) => mainWindow.ResetWindowPositions();
        menu.Add(resetPositionsItem);

        var showMiniWidgetItem = new NativeMenuItem("Show Mini Widget");
        showMiniWidgetItem.Click += (_, _) => miniWidgetService.Show();
        menu.Add(showMiniWidgetItem);

        menu.Add(new NativeMenuItemSeparator());

        var openItem = new NativeMenuItem("Open");
        openItem.Click += (_, _) => mainWindow.OpenFromTray();
        menu.Add(openItem);

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            mainWindow.PrepareForExit();
            desktop.Shutdown();
        };
        menu.Add(exitItem);

        using var iconStream = AssetLoader.Open(new Uri("avares://OpenNetMeter/Assets/x48.png"));
        trayIcon = new TrayIcon
        {
            ToolTipText = "OpenNetMeter",
            Menu = menu,
            Icon = new WindowIcon(iconStream),
            IsVisible = true
        };

        trayIcon.Clicked += (_, _) => mainWindow.OpenFromTray();
        TrayIcon.SetIcons(application, new TrayIcons { trayIcon });
    }

    public void Dispose()
    {
        try
        {
            trayIcon.IsVisible = false;
            trayIcon.Dispose();
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to dispose tray icon", ex);
        }
    }
}
