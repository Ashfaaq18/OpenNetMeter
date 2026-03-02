using OpenNetMeter.Core.ViewModels;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel
{
    public HistoryViewModel History { get; } = new();
    public SettingsViewModel Settings { get; } = new();
}
