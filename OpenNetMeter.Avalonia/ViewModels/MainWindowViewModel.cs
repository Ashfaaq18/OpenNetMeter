using System;
using System.Windows.Input;
using OpenNetMeter.Core.ViewModels;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel
{
    public event EventHandler? RequestMinimizeWindow;
    public event EventHandler? RequestCloseWindow;
    public event EventHandler? RequestAbout;

    public MainWindowViewModel()
    {
        SwitchTabCommand = new ParameterRelayCommand(parameter =>
        {
            if (parameter is null)
                return;

            if (int.TryParse(parameter.ToString(), out var nextIndex))
                SelectedTabIndex = nextIndex;
        });

        AboutCommand = new ActionCommand(() => RequestAbout?.Invoke(this, EventArgs.Empty));
        MinimizeWindowCommand = new ActionCommand(() => RequestMinimizeWindow?.Invoke(this, EventArgs.Empty));
        CloseWindowCommand = new ActionCommand(() => RequestCloseWindow?.Invoke(this, EventArgs.Empty));
    }

    public SummaryViewModel Summary { get; } = new();
    public HistoryViewModel History { get; } = new();
    public SettingsViewModel Settings { get; } = new();

    public ICommand SwitchTabCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand MinimizeWindowCommand { get; }
    public ICommand CloseWindowCommand { get; }

    public bool IsSummaryTab => SelectedTabIndex == 0;
    public bool IsHistoryTab => SelectedTabIndex == 1;
    public bool IsSettingsTab => SelectedTabIndex == 2;
    public string NetworkStatus => "Connected";

    public override int SelectedTabIndex
    {
        get => base.SelectedTabIndex;
        set
        {
            if (base.SelectedTabIndex == value)
                return;

            base.SelectedTabIndex = value;
            OnPropertyChanged(nameof(IsSummaryTab));
            OnPropertyChanged(nameof(IsHistoryTab));
            OnPropertyChanged(nameof(IsSettingsTab));
        }
    }

    private sealed class ActionCommand : ICommand
    {
        private readonly Action execute;

        public ActionCommand(Action execute)
        {
            this.execute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            execute();
        }
    }

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
