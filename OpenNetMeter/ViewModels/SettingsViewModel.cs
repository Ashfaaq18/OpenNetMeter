using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using OpenNetMeter.Models;
using OpenNetMeter.Services;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IMiniWidgetService miniWidgetService;
    private readonly IStartupRegistrationService startupRegistrationService;
    private readonly IExternalLinkService externalLinkService;
    private readonly IThemeService themeService;
    private readonly IFileSaveDialogService fileSaveDialogService;
    private bool startWithWindows;
    private bool minimizeOnStart;
    private bool darkMode;
    private bool miniWidgetVisible = true;
    private double miniWidgetTransparency = 20;
    private int selectedNetworkTargetIndex = 2;
    private int selectedSpeedMagnitudeIndex;
    private int selectedSpeedUnitIndex;
    private bool isCheckingForUpdates;
    private bool isUpdateAvailable;
    private string updateStatusMessage = "Click here to check for new updates";
    private string downloadUrl = string.Empty;
    private int selectedRetentionIndex;
    private bool isExporting;
    private bool isConfirmationOpen;
    private string confirmationMessage = string.Empty;
    private Action? pendingConfirm;
    private Action? pendingCancel;

    public SettingsViewModel(MiniWidgetViewModel miniWidgetViewModel, IMiniWidgetService miniWidgetService, IStartupRegistrationService startupRegistrationService, IExternalLinkService externalLinkService, IThemeService themeService, IFileSaveDialogService fileSaveDialogService, IFileRevealService fileRevealService)
    {
        ExportStatus = new ExportStatusViewModel(fileRevealService);
        this.miniWidgetService = miniWidgetService;
        this.startupRegistrationService = startupRegistrationService;
        this.externalLinkService = externalLinkService;
        this.themeService = themeService;
        this.fileSaveDialogService = fileSaveDialogService;
        var settings = SettingsManager.Current;
        startWithWindows = settings.StartWithWin;
        minimizeOnStart = settings.MinimizeOnStart;
        darkMode = settings.DarkMode;
        miniWidgetVisible = settings.MiniWidgetVisibility;
        miniWidgetTransparency = settings.MiniWidgetTransparentSlider;
        selectedNetworkTargetIndex = settings.NetworkType;
        selectedSpeedMagnitudeIndex = settings.NetworkSpeedMagnitude;
        selectedSpeedUnitIndex = settings.NetworkSpeedFormat;
        selectedRetentionIndex = HistoryRetention.IndexOf(settings.HistoryRetentionDays);

        ResetDataCommand = new RelayCommand(ResetData);
        ExportAllCommand = new RelayCommand(async () => await ExportAllAsync());
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
        DownloadUpdateCommand = new RelayCommand(DownloadUpdate);
        ConfirmCommand = new RelayCommand(Confirm);
        CancelConfirmationCommand = new RelayCommand(CancelConfirmation);

        this.miniWidgetService.VisibilityChanged += SyncMiniWidgetVisibility;
        this.themeService.ApplyDarkMode(darkMode);
        this.miniWidgetService.RefreshAppearance(darkMode, (int)Math.Round(miniWidgetTransparency));
    }

    public SettingsViewModel()
        : this(new MiniWidgetViewModel(), new PlaceholderMiniWidgetService(), new PlaceholderStartupRegistrationService(), new NoOpExternalLinkService(), new NoOpThemeService(), new PlaceholderFileSaveDialogService(), new PlaceholderFileRevealService())
    {
    }

    public bool StartWithWindows
    {
        get => startWithWindows;
        set
        {
            if (startWithWindows == value)
                return;

            startWithWindows = value;
            SettingsManager.Current.StartWithWin = value;
            SettingsManager.Save();
            startupRegistrationService.SetEnabled(value, MinimizeOnStart);
            OnPropertyChanged(nameof(StartWithWindows));
            OnPropertyChanged(nameof(CanChangeMinimizeOnStart));
        }
    }

    public bool MinimizeOnStart
    {
        get => minimizeOnStart;
        set
        {
            if (minimizeOnStart == value)
                return;
            minimizeOnStart = value;
            SettingsManager.Current.MinimizeOnStart = value;
            SettingsManager.Save();
            OnPropertyChanged(nameof(MinimizeOnStart));
        }
    }

    public bool CanChangeMinimizeOnStart => !StartWithWindows;

    public bool DarkMode
    {
        get => darkMode;
        set
        {
            if (darkMode == value)
                return;
            darkMode = value;
            SettingsManager.Current.DarkMode = value;
            SettingsManager.Save();
            themeService.ApplyDarkMode(value);
            miniWidgetService.RefreshAppearance(value, (int)Math.Round(MiniWidgetTransparency));
            OnPropertyChanged(nameof(DarkMode));
        }
    }

    public bool MiniWidgetVisible
    {
        get => miniWidgetVisible;
        set
        {
            if (miniWidgetVisible == value)
                return;
            miniWidgetVisible = value;
            SettingsManager.Current.MiniWidgetVisibility = value;
            SettingsManager.Save();
            if (value)
                miniWidgetService.Show();
            else
                miniWidgetService.Hide();
            OnPropertyChanged(nameof(MiniWidgetVisible));
        }
    }

    public double MiniWidgetTransparency
    {
        get => miniWidgetTransparency;
        set
        {
            if (miniWidgetTransparency.Equals(value))
                return;
            miniWidgetTransparency = value;
            SettingsManager.Current.MiniWidgetTransparentSlider = (int)Math.Round(value);
            SettingsManager.Save();
            miniWidgetService.RefreshAppearance(DarkMode, (int)Math.Round(value));
            OnPropertyChanged(nameof(MiniWidgetTransparency));
        }
    }

    public string[] NetworkTargets { get; } = ["Private", "Public", "Both"];

    public int SelectedNetworkTargetIndex
    {
        get => selectedNetworkTargetIndex;
        set
        {
            if (selectedNetworkTargetIndex == value)
                return;
            selectedNetworkTargetIndex = value;
            SettingsManager.Current.NetworkType = value;
            SettingsManager.Save();
            OnPropertyChanged(nameof(SelectedNetworkTargetIndex));
            OnPropertyChanged(nameof(IsNetworkTargetPrivate));
            OnPropertyChanged(nameof(IsNetworkTargetPublic));
            OnPropertyChanged(nameof(IsNetworkTargetBoth));
        }
    }

    public bool IsNetworkTargetPrivate
    {
        get => SelectedNetworkTargetIndex == 0;
        set
        {
            if (value)
                SelectedNetworkTargetIndex = 0;
        }
    }

    public bool IsNetworkTargetPublic
    {
        get => SelectedNetworkTargetIndex == 1;
        set
        {
            if (value)
                SelectedNetworkTargetIndex = 1;
        }
    }

    public bool IsNetworkTargetBoth
    {
        get => SelectedNetworkTargetIndex == 2;
        set
        {
            if (value)
                SelectedNetworkTargetIndex = 2;
        }
    }

    public string[] SpeedMagnitudes { get; } = ["Auto", "Kilo", "Mega", "Giga"];

    public int SelectedSpeedMagnitudeIndex
    {
        get => selectedSpeedMagnitudeIndex;
        set
        {
            if (selectedSpeedMagnitudeIndex == value)
                return;
            selectedSpeedMagnitudeIndex = value;
            SettingsManager.Current.NetworkSpeedMagnitude = value;
            SettingsManager.Save();
            OnPropertyChanged(nameof(SelectedSpeedMagnitudeIndex));
        }
    }

    public string[] SpeedUnits { get; } = ["bps (bits/sec)", "Bps (bytes/sec)"];

    public int SelectedSpeedUnitIndex
    {
        get => selectedSpeedUnitIndex;
        set
        {
            if (selectedSpeedUnitIndex == value)
                return;
            selectedSpeedUnitIndex = value;
            SettingsManager.Current.NetworkSpeedFormat = value;
            SettingsManager.Save();
            OnPropertyChanged(nameof(SelectedSpeedUnitIndex));
        }
    }

    public string[] RetentionOptions { get; } = HistoryRetention.Options.Select(o => o.Label).ToArray();

    public int SelectedRetentionIndex
    {
        get => selectedRetentionIndex;
        set
        {
            if (selectedRetentionIndex == value || value < 0 || value >= HistoryRetention.Options.Length)
                return;

            if (IsConfirmationOpen)
            {
                // Keyboard input can still reach the ComboBox behind the overlay; snap it back.
                OnPropertyChanged(nameof(SelectedRetentionIndex));
                return;
            }

            selectedRetentionIndex = value;
            NotifyRetentionSelectionChanged();

            var (label, newDays) = HistoryRetention.Options[value];
            if (!HistoryRetention.IsShorter(newDays, SettingsManager.Current.HistoryRetentionDays))
            {
                SaveRetention(newDays);
                return;
            }

            OpenConfirmation(
                $"Usage history older than {label} will be permanently deleted.\nDo you still want to continue?",
                onConfirm: () =>
                {
                    SaveRetention(newDays);
                    RetentionShortened?.Invoke();
                },
                onCancel: () =>
                {
                    selectedRetentionIndex = HistoryRetention.IndexOf(SettingsManager.Current.HistoryRetentionDays);
                    NotifyRetentionSelectionChanged();
                });
        }
    }

    public bool IsForeverRetentionSelected =>
        HistoryRetention.Options[selectedRetentionIndex].Days == HistoryRetention.Forever;

    public string ForeverRetentionWarning =>
        "History is never deleted, so the database keeps growing " +
        $"(currently {ByteSizeFormatter.FormatBytes(UsageDatabase.GetDatabaseSizeBytes())}). " +
        "Export or pick a shorter period if it gets large.";

    public ExportStatusViewModel ExportStatus { get; }

    public bool IsCheckingForUpdates
    {
        get => isCheckingForUpdates;
        private set
        {
            if (isCheckingForUpdates == value)
                return;
            isCheckingForUpdates = value;
            OnPropertyChanged(nameof(IsCheckingForUpdates));
        }
    }

    public bool IsUpdateAvailable
    {
        get => isUpdateAvailable;
        private set
        {
            if (isUpdateAvailable == value)
                return;
            isUpdateAvailable = value;
            OnPropertyChanged(nameof(IsUpdateAvailable));
        }
    }

    public string UpdateStatusMessage
    {
        get => updateStatusMessage;
        private set
        {
            if (updateStatusMessage == value)
                return;
            updateStatusMessage = value;
            OnPropertyChanged(nameof(UpdateStatusMessage));
        }
    }

    public bool IsConfirmationOpen
    {
        get => isConfirmationOpen;
        private set
        {
            if (isConfirmationOpen == value)
                return;
            isConfirmationOpen = value;
            OnPropertyChanged(nameof(IsConfirmationOpen));
        }
    }

    public string ConfirmationMessage
    {
        get => confirmationMessage;
        private set
        {
            if (confirmationMessage == value)
                return;
            confirmationMessage = value;
            OnPropertyChanged(nameof(ConfirmationMessage));
        }
    }

    public ICommand ResetDataCommand { get; }
    public ICommand ExportAllCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand DownloadUpdateCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelConfirmationCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? DeleteAllDataConfirmed;
    public event Action? RetentionShortened;

    public void SyncMiniWidgetVisibility(bool isVisible)
    {
        if (miniWidgetVisible == isVisible)
            return;

        miniWidgetVisible = isVisible;
        SettingsManager.Current.MiniWidgetVisibility = isVisible;
        SettingsManager.Save();
        OnPropertyChanged(nameof(MiniWidgetVisible));
    }

    private void ResetData()
    {
        if (IsConfirmationOpen)
            return;

        OpenConfirmation(
            "Warning!!! This will delete all saved profiles.\nDo you still want to continue?",
            onConfirm: () => DeleteAllDataConfirmed?.Invoke());
    }

    private void NotifyRetentionSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedRetentionIndex));
        OnPropertyChanged(nameof(IsForeverRetentionSelected));
        OnPropertyChanged(nameof(ForeverRetentionWarning));
    }

    private static void SaveRetention(int days)
    {
        SettingsManager.Current.HistoryRetentionDays = days;
        SettingsManager.Save();
    }

    private async Task ExportAllAsync()
    {
        if (isExporting)
            return;

        isExporting = true;
        ExportStatus.Clear();
        try
        {
            string? path = await fileSaveDialogService.PickSavePathAsync(
                "Export usage data",
                $"OpenNetMeter-usage-{DateTime.Today:yyyyMMdd}",
                UsageExporter.FileTypes);
            if (string.IsNullOrWhiteSpace(path))
                return;

            ExportStatus.ShowProgress();
            int count = await UsageExporter.ExportAsync(UsageDatabase.ResolveDatabasePath(), path);
            ExportStatus.ShowSuccess(count, path);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Error exporting usage data", ex);
            ExportStatus.ShowError();
        }
        finally
        {
            isExporting = false;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusMessage = "Checking for updates...";
        IsUpdateAvailable = false;
        downloadUrl = string.Empty;
        string statusMessage = string.Empty;

        const int minDisplayTimeMs = 2000;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            (Version? latestVersion, string? nextDownloadUrl) = await UpdateChecker.CheckForUpdatesAsync();
            if (latestVersion != null && nextDownloadUrl != null)
            {
                Version? currentVersion = Assembly.GetExecutingAssembly()?.GetName()?.Version;
                if (currentVersion != null && latestVersion > currentVersion)
                {
                    downloadUrl = nextDownloadUrl;
                    statusMessage = $"A new version {latestVersion} is available!";
                    IsUpdateAvailable = true;
                }
                else
                {
                    statusMessage = "You have the latest version.";
                }
            }
            else
            {
                statusMessage = "Error checking for updates.";
            }
        }
        catch (Exception ex)
        {
            statusMessage = "Error checking for updates.";
            EventLogger.Error("Error checking for updates", ex);
        }
        finally
        {
            stopwatch.Stop();

            int remainingTime = minDisplayTimeMs - (int)stopwatch.ElapsedMilliseconds;
            if (remainingTime > 0)
            {
                await Task.Delay(remainingTime);
            }

            IsCheckingForUpdates = false;
            UpdateStatusMessage = statusMessage;
        }
    }

    private void DownloadUpdate()
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            UpdateStatusMessage = "No update download is available.";
            return;
        }

        try
        {
            externalLinkService.Open(downloadUrl);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Error launching update download URL", ex);
            UpdateStatusMessage = "Error launching update download URL.";
        }
    }

    private void OpenConfirmation(string message, Action onConfirm, Action? onCancel = null)
    {
        pendingConfirm = onConfirm;
        pendingCancel = onCancel;
        ConfirmationMessage = message;
        IsConfirmationOpen = true;
    }

    private void Confirm()
    {
        var action = pendingConfirm;
        CloseConfirmation();
        action?.Invoke();
    }

    private void CancelConfirmation()
    {
        var action = pendingCancel;
        CloseConfirmation();
        action?.Invoke();
    }

    private void CloseConfirmation()
    {
        pendingConfirm = null;
        pendingCancel = null;
        IsConfirmationOpen = false;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class NoOpExternalLinkService : IExternalLinkService
    {
        public void Open(string uri)
        {
        }
    }
}

