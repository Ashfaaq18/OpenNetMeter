using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
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

    public SettingsViewModel()
    {
        ResetDataCommand = new RelayCommand(ResetData);
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
        DownloadUpdateCommand = new RelayCommand(DownloadUpdate);
    }

    public bool StartWithWindows
    {
        get => startWithWindows;
        set
        {
            if (startWithWindows == value)
                return;

            startWithWindows = value;
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
            OnPropertyChanged(nameof(SelectedNetworkTargetIndex));
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
