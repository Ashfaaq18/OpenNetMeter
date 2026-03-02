using OpenNetMeter.Core.ViewModels;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel
{
    public SummaryViewModel Summary { get; } = new();
    public HistoryViewModel History { get; } = new();
    public SettingsViewModel Settings { get; } = new();
}
