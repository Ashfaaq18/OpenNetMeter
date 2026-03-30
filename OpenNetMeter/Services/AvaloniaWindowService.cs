using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Services;

public sealed class AvaloniaWindowService : IWindowService
{
    public void MinimizeMainWindow()
    {
        if (GetMainWindow() is { } window)
            window.WindowState = WindowState.Minimized;
    }

    public void CloseMainWindow()
    {
        if (GetMainWindow() is { } window)
            window.Close();
    }

    public void ShowAbout()
    {
        // Placeholder until About dialog content is migrated to Avalonia.
    }

    private static Window? GetMainWindow()
    {
        return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    }
}

