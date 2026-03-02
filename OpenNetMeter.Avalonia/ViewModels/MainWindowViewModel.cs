using OpenNetMeter.Core.ViewModels;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel
{
    public SettingsViewModel Settings { get; } = new();
}
