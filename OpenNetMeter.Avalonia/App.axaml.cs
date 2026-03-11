using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenNetMeter.Avalonia.Services;
using OpenNetMeter.Avalonia.ViewModels;
using OpenNetMeter.Avalonia.Views;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var windowService = new AvaloniaWindowService();
            INetworkCaptureService networkCaptureService = OperatingSystem.IsWindows()
                ? new WindowsNetworkCaptureService()
                : new PlaceholderNetworkCaptureService();
            IProcessIconService processIconService = OperatingSystem.IsWindows()
                ? new WindowsProcessIconService()
                : new PlaceholderProcessIconService();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(windowService, networkCaptureService, processIconService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
