using System;
using System.Windows.Input;
using OpenNetMeter.Core.ViewModels;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel, IDisposable
{
    private readonly IWindowService windowService;
    private readonly INetworkCaptureService networkCaptureService;
    private string networkStatus = "Disconnected";

    public MainWindowViewModel()
        : this(new NoOpWindowService(), new NoOpNetworkCaptureService())
    {
    }

    public MainWindowViewModel(IWindowService windowService, INetworkCaptureService networkCaptureService)
    {
        this.windowService = windowService;
        this.networkCaptureService = networkCaptureService;

        Summary = new SummaryViewModel(this.networkCaptureService);
        History = new HistoryViewModel();
        Settings = new SettingsViewModel();

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

        this.networkCaptureService.NetworkChanged += OnNetworkChanged;
        this.networkCaptureService.Start();
    }

    public SummaryViewModel Summary { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    public ICommand SwitchTabCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand MinimizeWindowCommand { get; }
    public ICommand CloseWindowCommand { get; }

    public bool IsSummaryTab => SelectedTabIndex == 0;
    public bool IsHistoryTab => SelectedTabIndex == 1;
    public bool IsSettingsTab => SelectedTabIndex == 2;

    public string NetworkStatus
    {
        get => networkStatus;
        private set
        {
            if (networkStatus == value)
                return;
            networkStatus = value;
            OnPropertyChanged(nameof(NetworkStatus));
        }
    }

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

    public void Dispose()
    {
        Summary.Dispose();
        networkCaptureService.NetworkChanged -= OnNetworkChanged;
        networkCaptureService.Dispose();
    }

    private void OnNetworkChanged(object? sender, NetworkSnapshotChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.AdapterName))
        {
            NetworkStatus = "Disconnected";
            Summary.ClearOnDisconnect();
            return;
        }

        NetworkStatus = $"Connected : {e.AdapterName}";
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
        public void MinimizeMainWindow() { }
        public void CloseMainWindow() { }
        public void ShowAbout() { }
    }

    private sealed class NoOpNetworkCaptureService : INetworkCaptureService
    {
        public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged
        {
            add { }
            remove { }
        }
        public event EventHandler<NetworkTrafficEventArgs>? TrafficObserved
        {
            add { }
            remove { }
        }

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
