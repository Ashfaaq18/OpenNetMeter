using System;
using System.Reflection;
using System.Windows.Input;
using OpenNetMeter.Avalonia.Services;
using OpenNetMeter.Core.ViewModels;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel, IDisposable
{
    private const string AboutRepositoryUri = "https://github.com/Ashfaaq18/OpenNetMeter";

    private readonly IWindowService windowService;
    private readonly INetworkCaptureService networkCaptureService;
    private readonly IExternalLinkService externalLinkService;
    private readonly MiniWidgetViewModel miniWidget;
    private string networkStatus = "Disconnected";
    private bool isAboutOpen;

    public MainWindowViewModel()
        : this(new NoOpWindowService(), new NoOpNetworkCaptureService(), new NoOpProcessIconService(), new NoOpExternalLinkService(), new MiniWidgetViewModel(), new PlaceholderMiniWidgetService(), new PlaceholderStartupRegistrationService())
    {
    }

    public MainWindowViewModel(
        IWindowService windowService,
        INetworkCaptureService networkCaptureService,
        IProcessIconService processIconService,
        IExternalLinkService externalLinkService,
        MiniWidgetViewModel miniWidget,
        IMiniWidgetService miniWidgetService,
        IStartupRegistrationService startupRegistrationService)
    {
        this.windowService = windowService;
        this.networkCaptureService = networkCaptureService;
        this.externalLinkService = externalLinkService;
        this.miniWidget = miniWidget;

        Summary = new SummaryViewModel(this.networkCaptureService, processIconService);
        History = new HistoryViewModel(processIconService);
        Settings = new SettingsViewModel(miniWidget, miniWidgetService, startupRegistrationService, externalLinkService);
        Settings.PropertyChanged += Settings_PropertyChanged;
        Summary.PropertyChanged += Summary_PropertyChanged;

        SwitchTabCommand = new ParameterRelayCommand(parameter =>
        {
            if (parameter is null)
                return;

            if (int.TryParse(parameter.ToString(), out var nextIndex))
                SelectedTabIndex = nextIndex;
        });

        AboutCommand = new RelayCommand(() => IsAboutOpen = true);
        CloseAboutCommand = new RelayCommand(() => IsAboutOpen = false);
        OpenAboutRepositoryCommand = new RelayCommand(() => this.externalLinkService.Open(AboutRepositoryUri));
        MinimizeWindowCommand = new RelayCommand(() => this.windowService.MinimizeMainWindow());
        CloseWindowCommand = new RelayCommand(() => this.windowService.CloseMainWindow());

        this.networkCaptureService.NetworkChanged += OnNetworkChanged;
        this.networkCaptureService.Start();
        SyncMiniWidgetFromSummary();
    }

    public SummaryViewModel Summary { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }
    public string AboutVersionText { get; } = $"Version: {Assembly.GetExecutingAssembly()?.GetName().Version}";
    public string AboutRepositoryUrl { get; } = AboutRepositoryUri;

    public ICommand SwitchTabCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand CloseAboutCommand { get; }
    public ICommand OpenAboutRepositoryCommand { get; }
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

    public bool IsAboutOpen
    {
        get => isAboutOpen;
        private set
        {
            if (isAboutOpen == value)
                return;

            isAboutOpen = value;
            OnPropertyChanged(nameof(IsAboutOpen));
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
        Settings.PropertyChanged -= Settings_PropertyChanged;
        Summary.PropertyChanged -= Summary_PropertyChanged;
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
        Summary.SetActiveAdapter(e.AdapterName);
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedSpeedMagnitudeIndex) ||
            e.PropertyName == nameof(SettingsViewModel.SelectedSpeedUnitIndex))
        {
            Summary.RefreshSpeedDisplayFormat();
        }
    }

    private void Summary_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SummaryViewModel.DownloadSpeedText):
            case nameof(SummaryViewModel.UploadSpeedText):
            case nameof(SummaryViewModel.CurrentSessionDownloadText):
            case nameof(SummaryViewModel.CurrentSessionUploadText):
                SyncMiniWidgetFromSummary();
                break;
        }
    }

    private void SyncMiniWidgetFromSummary()
    {
        miniWidget.DownloadSpeedText = Summary.DownloadSpeedText;
        miniWidget.UploadSpeedText = Summary.UploadSpeedText;
        miniWidget.CurrentSessionDownloadText = Summary.CurrentSessionDownloadText;
        miniWidget.CurrentSessionUploadText = Summary.CurrentSessionUploadText;
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

    private sealed class NoOpProcessIconService : IProcessIconService
    {
        public object? GetProcessIcon(string processName) => null;
    }

    private sealed class NoOpExternalLinkService : IExternalLinkService
    {
        public void Open(string uri) { }
    }
}
