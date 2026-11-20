using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using OpenNetMeter.Models;
using OpenNetMeter.Properties;
using OpenNetMeter.Services;
using OpenNetMeter.Utilities;
using OpenNetMeter.Core.ViewModels;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.ViewModels;

public sealed class MainWindowViewModel : MainShellTabsViewModel, IDisposable
{
    private const string AboutRepositoryUri = "https://github.com/Ashfaaq18/OpenNetMeter";

    private readonly IWindowService windowService;
    private readonly INetworkCaptureService networkCaptureService;
    private readonly IExternalLinkService externalLinkService;
    private readonly MiniWidgetViewModel miniWidget;
    private readonly MiniWidgetSync miniWidgetSync;
    private readonly SummaryActivityGate summaryActivity;
    private string networkStatus = "Disconnected";
    private string connectedAdapterName = string.Empty;
    private string pinnedAdapterWarning = string.Empty;
    private bool isAboutOpen;

    public MainWindowViewModel()
        : this(new NoOpWindowService(), new NoOpNetworkCaptureService(), new NoOpProcessIconService(), new NoOpExternalLinkService(), new MiniWidgetViewModel(), new PlaceholderMiniWidgetService(), new PlaceholderStartupRegistrationService(), new NoOpThemeService(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService())
    {
    }

    public MainWindowViewModel(
        IWindowService windowService,
        INetworkCaptureService networkCaptureService,
        IProcessIconService processIconService,
        IExternalLinkService externalLinkService,
        MiniWidgetViewModel miniWidget,
        IMiniWidgetService miniWidgetService,
        IStartupRegistrationService startupRegistrationService,
        IThemeService themeService,
        IFileSaveDialogService fileSaveDialogService,
        IFileRevealService fileRevealService)
    {
        this.windowService = windowService;
        this.networkCaptureService = networkCaptureService;
        this.externalLinkService = externalLinkService;
        this.miniWidget = miniWidget;

        Summary = new SummaryViewModel(this.networkCaptureService, processIconService, externalLinkService, fileSaveDialogService, fileRevealService);
        History = new HistoryViewModel(processIconService, externalLinkService, fileSaveDialogService, fileRevealService);
        Settings = new SettingsViewModel(miniWidget, miniWidgetService, startupRegistrationService, externalLinkService, themeService, fileSaveDialogService, fileRevealService, this.networkCaptureService);
        Settings.PropertyChanged += Settings_PropertyChanged;
        Settings.DeleteAllDataConfirmed += Settings_DeleteAllDataConfirmed;
        Settings.RetentionShortened += Settings_RetentionShortened;
        Summary.PropertyChanged += Summary_PropertyChanged;
        // The widget follows the Summary's per-flush event, not its property changes (which are held back while the
        // window is hidden), so it keeps updating every second whatever the window is doing.
        miniWidgetSync = new MiniWidgetSync(Summary, () => Settings.IsMiniWidgetTotalUsageFrom, ApplyToMiniWidget);
        // The window usually isn't showing at startup, so the Summary starts out inactive; MainWindow reports changes.
        summaryActivity = new SummaryActivityGate(Summary);

        Summary.SetWidgetUsageFrom(Settings.EffectiveMiniWidgetUsageFromDate, Settings.MiniWidgetUsageFromFollowsToday);
        miniWidget.SetUsageFromTotal(Settings.IsMiniWidgetTotalUsageFrom);
        miniWidget.SetTotalActions(
            usageFrom => Settings.MiniWidgetTotalMode = usageFrom ? MiniWidgetUsageFrom.ModeUsageFrom : MiniWidgetUsageFrom.ModeSession,
            ChooseWidgetUsageFromDate);

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
        OpenAboutSupportCommand = new RelayCommand(() => this.externalLinkService.Open(ExternalLinks.SupportUri));
        MinimizeWindowCommand = new RelayCommand(() => this.windowService.MinimizeMainWindow());
        CloseWindowCommand = new RelayCommand(() => this.windowService.CloseMainWindow());

        this.networkCaptureService.NetworkChanged += OnNetworkChanged;
        CatchUpWithCaptureSnapshot();
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
    public ICommand OpenAboutSupportCommand { get; }
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
            OnPropertyChanged(nameof(IsNetworkConnected));
            OnPropertyChanged(nameof(NetworkStatusTooltip));
        }
    }

    public bool IsNetworkConnected => !string.Equals(NetworkStatus, "Disconnected", StringComparison.OrdinalIgnoreCase);

    public string NetworkStatusTooltip => IsNetworkConnected ? $"Monitoring {connectedAdapterName}" : "No active adapter";

    /// <summary>
    /// Shown in the status bar while a pinned adapter is selected but not connected; empty otherwise.
    /// </summary>
    public string PinnedAdapterWarning
    {
        get => pinnedAdapterWarning;
        private set
        {
            if (pinnedAdapterWarning == value)
                return;
            pinnedAdapterWarning = value;
            OnPropertyChanged(nameof(PinnedAdapterWarning));
            OnPropertyChanged(nameof(HasPinnedAdapterWarning));
        }
    }

    public bool HasPinnedAdapterWarning => pinnedAdapterWarning.Length > 0;

    public bool IsAboutOpen
    {
        get => isAboutOpen;
        private set
        {
            if (isAboutOpen == value)
                return;

            isAboutOpen = value;
            OnPropertyChanged(nameof(IsAboutOpen));
            OnPropertyChanged(nameof(IsModalOpen));
        }
    }

    /// <summary>
    /// True while an in-window dialog is shown; the rest of the window is disabled so focus can't escape to it.
    /// </summary>
    public bool IsModalOpen => IsAboutOpen || Settings.IsConfirmationOpen || Summary.IsRecordingResultsOpen;

    public override int SelectedTabIndex
    {
        get => base.SelectedTabIndex;
        set
        {
            if (base.SelectedTabIndex == value)
                return;

            var wasHistoryTab = IsHistoryTab;
            base.SelectedTabIndex = value;
            OnPropertyChanged(nameof(IsSummaryTab));
            OnPropertyChanged(nameof(IsHistoryTab));
            OnPropertyChanged(nameof(IsSettingsTab));
            summaryActivity.SetSummaryTabSelected(IsSummaryTab);

            // History only reads the database while it is showing; network changes and purges that happen
            // meanwhile are applied when the tab is next opened.
            if (wasHistoryTab)
                History.Deactivate();
            if (IsHistoryTab)
                History.Activate();

            // Network changes only refresh the adapter list while Settings is open, so catch up on arrival.
            if (IsSettingsTab)
                Settings.RefreshAdapterChoices();
        }
    }

    /// <summary>
    /// Called by the main window when it becomes visible or hidden, minimized or restored. While it isn't showing,
    /// the Summary holds back the notifications nothing is displaying (and the widget, which doesn't depend on the
    /// window, keeps updating from the per-flush event).
    /// </summary>
    public void SetWindowActive(bool active) => summaryActivity.SetWindowActive(active);

    /// <summary>
    /// Called on the UI thread once the capture start that App began in the background has returned, whether or
    /// not it succeeded.
    /// </summary>
    public void OnCaptureStartCompleted()
    {
        // Start() raises no NetworkChanged when nothing matches at launch (e.g. a pinned adapter that
        // is unplugged), so evaluate the warning once after any queued connect event has run.
        UpdatePinnedAdapterWarning();

        Settings.RunDeferredStartupWork();
    }

    public void Dispose()
    {
        Settings.PropertyChanged -= Settings_PropertyChanged;
        Settings.DeleteAllDataConfirmed -= Settings_DeleteAllDataConfirmed;
        Settings.RetentionShortened -= Settings_RetentionShortened;
        Summary.PropertyChanged -= Summary_PropertyChanged;
        miniWidgetSync.Dispose();
        Summary.Dispose();
        networkCaptureService.NetworkChanged -= OnNetworkChanged;
        networkCaptureService.Dispose();
    }

    private void OnNetworkChanged(object? sender, NetworkSnapshotChangedEventArgs e)
    {
        var adapterName = e.AdapterName;
        Dispatcher.UIThread.Post(() => ApplyNetworkSnapshot(adapterName));
    }

    /// <summary>
    /// Capture starts before this view model exists, so NetworkChanged may already have fired. The same snapshot can
    /// then arrive twice (here and through the event); applying it again changes nothing.
    /// </summary>
    private void CatchUpWithCaptureSnapshot()
    {
        // Read when the post runs, not now: a newer event posted in between would otherwise be overwritten by this
        // older snapshot. The latest event's own post always runs last, so the final state is the latest snapshot.
        Dispatcher.UIThread.Post(() =>
        {
            if (networkCaptureService.CurrentSnapshot is { } snapshot)
                ApplyNetworkSnapshot(snapshot.AdapterName);
        });
    }

    private void ApplyNetworkSnapshot(string adapterName)
    {
        if (string.IsNullOrWhiteSpace(adapterName))
        {
            NetworkStatus = "Disconnected";
            UpdatePinnedAdapterWarning();
            Summary.ClearOnDisconnect();
            RefreshAdapterChoicesIfVisible();
            return;
        }

        connectedAdapterName = adapterName;
        NetworkStatus = $"Connected : {adapterName}";
        UpdatePinnedAdapterWarning();
        Summary.SetActiveAdapter(adapterName);
        // History opens on the connected adapter's profile the first time it is shown.
        History.PreferredProfile = adapterName;
        History.ReloadProfiles();
        RefreshAdapterChoicesIfVisible();
    }

    /// <summary>
    /// Enumerating adapters reads the event log and rebuilding the list closes an open dropdown,
    /// so it is only done while the Settings tab is showing (and again when switching to it).
    /// </summary>
    private void RefreshAdapterChoicesIfVisible()
    {
        if (IsSettingsTab)
            Settings.RefreshAdapterChoices();
    }

    /// <summary>
    /// Warns when a pinned adapter is selected but not connected (nothing is counted then).
    /// Clears when connected or when selection is Automatic.
    /// </summary>
    private void UpdatePinnedAdapterWarning()
    {
        var settings = SettingsManager.Current;
        PinnedAdapterWarning = PinnedAdapterStatus.GetWarningText(settings.NetworkAdapterId, settings.NetworkAdapterName, IsNetworkConnected);
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedSpeedMagnitudeIndex) ||
            e.PropertyName == nameof(SettingsViewModel.SelectedSpeedUnitIndex))
        {
            Summary.RefreshSpeedDisplayFormat();
        }
        else if (e.PropertyName == nameof(SettingsViewModel.IsConfirmationOpen))
        {
            OnPropertyChanged(nameof(IsModalOpen));
        }
        else if (e.PropertyName == nameof(SettingsViewModel.SelectedAdapterChoice))
        {
            // Automatic clears the warning immediately; a pinned adapter warns while disconnected.
            // The re-evaluation the selection triggers updates it again through NetworkChanged.
            UpdatePinnedAdapterWarning();
        }
        else if (e.PropertyName == nameof(SettingsViewModel.MiniWidgetTotalMode))
        {
            miniWidget.SetUsageFromTotal(Settings.IsMiniWidgetTotalUsageFrom);
            SyncMiniWidgetFromSummary();
        }
        else if (e.PropertyName == nameof(SettingsViewModel.MiniWidgetUsageFromDate))
        {
            // Summary raises WidgetStateChanged once the read lands, which re-syncs the widget.
            Summary.SetWidgetUsageFrom(Settings.EffectiveMiniWidgetUsageFromDate, Settings.MiniWidgetUsageFromFollowsToday);
        }
    }

    /// <summary>Widget menu "Choose date…": switch to usage-from mode and open Settings to pick the date.</summary>
    private void ChooseWidgetUsageFromDate()
    {
        Settings.MiniWidgetTotalMode = MiniWidgetUsageFrom.ModeUsageFrom;
        SelectedTabIndex = 2;
        miniWidget.OpenMainWindowCommand.Execute(null);
    }

    private void Settings_DeleteAllDataConfirmed()
    {
        bool wasConnected = IsNetworkConnected;

        if (wasConnected)
        {
            networkCaptureService.Stop();
            Summary.ClearOnDisconnect();
            NetworkStatus = "Disconnected";
        }

        History.DeleteAllDbFiles();

        // Disconnected totals are held rather than zeroed, so re-read the now-empty DB explicitly.
        Summary.RefreshAfterHistoryPurge();

        if (wasConnected)
        {
            networkCaptureService.Start();
        }

        History.ReloadProfiles();
    }

    private async void Settings_RetentionShortened()
    {
        try
        {
            await Task.Run(ApplicationDB.ApplyRetention);
            History.Refresh();
            Summary.RefreshAfterHistoryPurge();
        }
        catch (Exception ex)
        {
            EventLogger.Error("Failed to apply history retention", ex);
        }
    }

    private void Summary_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // The widget's texts are not synced from here: see miniWidgetSync.
        if (e.PropertyName == nameof(SummaryViewModel.IsRecordingResultsOpen))
            OnPropertyChanged(nameof(IsModalOpen));
    }

    private void SyncMiniWidgetFromSummary() => miniWidgetSync.Sync();

    private void ApplyToMiniWidget(MiniWidgetTexts texts)
    {
        miniWidget.DownloadSpeedText = texts.DownloadSpeedText;
        miniWidget.UploadSpeedText = texts.UploadSpeedText;
        miniWidget.UsageFromMenuHeader = texts.UsageFromMenuHeader;
        miniWidget.TotalLabel = texts.TotalLabel;
        miniWidget.TotalToolTip = texts.TotalToolTip;
        miniWidget.TotalDownloadText = texts.TotalDownloadText;
        miniWidget.TotalUploadText = texts.TotalUploadText;
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
        public event EventHandler<NetworkTrafficBatchEventArgs>? TrafficBatchObserved
        {
            add { }
            remove { }
        }

        public NetworkSnapshotChangedEventArgs? CurrentSnapshot => null;

        public void Start() { }
        public void Stop() { }
        public IReadOnlyList<NetworkAdapterOption> GetAvailableAdapters() => [];
        public void RefreshAdapterSelection() { }
        public void Dispose() { }
    }

    private sealed class NoOpProcessIconService : IProcessIconService
    {
        public object? GetCachedIcon(string processName) => null;
        public Task<object?> LoadIconAsync(string processName) => Task.FromResult<object?>(null);
    }

    private sealed class NoOpExternalLinkService : IExternalLinkService
    {
        public void Open(string uri) { }
    }
}

