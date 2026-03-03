using System;
using System.Windows.Input;
using OpenNetMeter.Core.ViewModels;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel
{
    private readonly IWindowService windowService;

    public MainWindowViewModel()
        : this(new NoOpWindowService())
    {
    }

    public MainWindowViewModel(IWindowService windowService)
    {
        this.windowService = windowService;

        SwitchTabCommand = new ParameterRelayCommand(parameter =>
        {
            if (parameter is null)
                return;

            if (int.TryParse(parameter.ToString(), out var nextIndex))
                SelectedTabIndex = nextIndex;
        });

        AboutCommand = new RelayCommand(() => this.windowService.ShowAbout());
        MinimizeWindowCommand = new RelayCommand(() => this.windowService.MinimizeMainWindow());
        CloseWindowCommand = new RelayCommand(() => this.windowService.CloseMainWindow());
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

    private sealed class ParameterRelayCommand : ICommand
    {
        private readonly Action<object?> execute;

        public ParameterRelayCommand(Action<object?> execute)
        {
            this.execute = execute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            execute(parameter);
        }
    }

    private sealed class NoOpWindowService : IWindowService
    {
        public void MinimizeMainWindow()
        {
        }

        public void CloseMainWindow()
        {
        }

        public void ShowAbout()
        {
        }
    }
}
