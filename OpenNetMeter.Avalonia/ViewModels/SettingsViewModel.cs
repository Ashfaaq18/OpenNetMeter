using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using OpenNetMeter.Avalonia.Services;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IMiniWidgetService miniWidgetService;
    private readonly IStartupRegistrationService startupRegistrationService;
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
    private string updateStatusMessage = "Click to check for updates";

    public SettingsViewModel(MiniWidgetViewModel miniWidgetViewModel, IMiniWidgetService miniWidgetService, IStartupRegistrationService startupRegistrationService)
    {
        this.miniWidgetService = miniWidgetService;
        this.startupRegistrationService = startupRegistrationService;
        var settings = SettingsManager.Current;
        startWithWindows = settings.StartWithWin;
        minimizeOnStart = settings.MinimizeOnStart;
        darkMode = settings.DarkMode;
        miniWidgetVisible = settings.MiniWidgetVisibility;
        miniWidgetTransparency = settings.MiniWidgetTransparentSlider;
        selectedNetworkTargetIndex = settings.NetworkType;
        selectedSpeedMagnitudeIndex = settings.NetworkSpeedMagnitude;
        selectedSpeedUnitIndex = settings.NetworkSpeedFormat;

        ResetDataCommand = new RelayCommand(ResetData);
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
        DownloadUpdateCommand = new RelayCommand(DownloadUpdate);

        this.miniWidgetService.VisibilityChanged += SyncMiniWidgetVisibility;
        this.miniWidgetService.RefreshAppearance(darkMode, (int)Math.Round(miniWidgetTransparency));
    }

    public SettingsViewModel()
        : this(new MiniWidgetViewModel(), new PlaceholderMiniWidgetService(), new PlaceholderStartupRegistrationService())
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

    public ICommand ResetDataCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }
    public ICommand DownloadUpdateCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

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
        UpdateStatusMessage = "Reset requested (placeholder).";
    }

    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        IsUpdateAvailable = false;
        UpdateStatusMessage = "Checking for updates...";
        await Task.Delay(1200);
        IsCheckingForUpdates = false;
        IsUpdateAvailable = true;
        UpdateStatusMessage = "New update available (placeholder).";
    }

    private void DownloadUpdate()
    {
        UpdateStatusMessage = "Download triggered (placeholder).";
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
