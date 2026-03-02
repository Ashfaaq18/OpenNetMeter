using System;
using System.IO;
using System.Windows.Input;
using OpenNetMeter.Core.ViewModels;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel
{
    public MainWindowViewModel()
    {
        AppIconExists = File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png"));
        SwitchTabCommand = new ParameterRelayCommand(parameter =>
        {
            if (parameter is null)
                return;

            if (int.TryParse(parameter.ToString(), out var nextIndex))
                SelectedTabIndex = nextIndex;
        });
    }

    public SummaryViewModel Summary { get; } = new();
    public HistoryViewModel History { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    public ICommand SwitchTabCommand { get; }
    public bool AppIconExists { get; }
    public bool IsSummaryTab => SelectedTabIndex == 0;
    public bool IsHistoryTab => SelectedTabIndex == 1;
    public bool IsSettingsTab => SelectedTabIndex == 2;
    public string NetworkStatus => "Connected";

    private sealed class ParameterRelayCommand : ICommand
    {
        private readonly Action<object?> execute;

        public ParameterRelayCommand(Action<object?> execute)
        {
            this.execute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            execute(parameter);
        }
    }
}
