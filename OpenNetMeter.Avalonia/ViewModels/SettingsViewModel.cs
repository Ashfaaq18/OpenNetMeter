using System.ComponentModel;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private bool startWithWindows;
    private bool minimizeOnStart;
    private bool darkMode;
    private bool miniWidgetVisible = true;
    private double miniWidgetTransparency = 20;
    private int selectedNetworkTargetIndex = 2;
    private int selectedSpeedUnitIndex;

    public bool StartWithWindows
    {
        get => startWithWindows;
        set
        {
            if (startWithWindows == value)
                return;
            startWithWindows = value;
            OnPropertyChanged(nameof(StartWithWindows));
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
