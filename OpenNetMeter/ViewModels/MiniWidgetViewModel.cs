using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OpenNetMeter.Properties;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

public sealed class MiniWidgetViewModel : INotifyPropertyChanged
{
    private static readonly Bitmap PinLightImage = LoadBitmap("avares://OpenNetMeter/Assets/pin/pin.png");
    private static readonly Bitmap PinDarkImage = LoadBitmap("avares://OpenNetMeter/Assets/pin/pin-dark.png");
    private static readonly Bitmap UnpinLightImage = LoadBitmap("avares://OpenNetMeter/Assets/pin/unpin.png");
    private static readonly Bitmap UnpinDarkImage = LoadBitmap("avares://OpenNetMeter/Assets/pin/unpin-dark.png");

    private string downloadSpeedText = "35.2 Mbps";
    private string uploadSpeedText = "4.8 Mbps";
    private string totalDownloadText = "1.24 GB";
    private string totalUploadText = "98.4 MB";
    private string totalLabel = "Total:";
    private string totalToolTip = "Current session";
    private bool isUsageFromTotal;
    private string usageFromMenuHeader = "Usage from today";
    private Action<bool> selectUsageFromTotal = _ => { };
    private string backgroundColor = "#cc252525";
    private bool isPinned;
    private bool darkMode;
    private ICommand togglePinnedCommand;

    public string DownloadSpeedText
    {
        get => downloadSpeedText;
        set
        {
            if (downloadSpeedText == value)
                return;
            downloadSpeedText = value;
            OnPropertyChanged(nameof(DownloadSpeedText));
        }
    }

    public string UploadSpeedText
    {
        get => uploadSpeedText;
        set
        {
            if (uploadSpeedText == value)
                return;
            uploadSpeedText = value;
            OnPropertyChanged(nameof(UploadSpeedText));
        }
    }

    /// <summary>Download total: the current session, or usage since the widget's chosen date.</summary>
    public string TotalDownloadText
    {
        get => totalDownloadText;
        set
        {
            if (totalDownloadText == value)
                return;
            totalDownloadText = value;
            OnPropertyChanged(nameof(TotalDownloadText));
        }
    }

    /// <summary>Upload total: the current session, or usage since the widget's chosen date.</summary>
    public string TotalUploadText
    {
        get => totalUploadText;
        set
        {
            if (totalUploadText == value)
                return;
            totalUploadText = value;
            OnPropertyChanged(nameof(TotalUploadText));
        }
    }

    public string TotalLabel
    {
        get => totalLabel;
        set
        {
            if (totalLabel == value)
                return;
            totalLabel = value;
            OnPropertyChanged(nameof(TotalLabel));
        }
    }

    public string TotalToolTip
    {
        get => totalToolTip;
        set
        {
            if (totalToolTip == value)
                return;
            totalToolTip = value;
            OnPropertyChanged(nameof(TotalToolTip));
        }
    }

    public string UsageFromMenuHeader
    {
        get => usageFromMenuHeader;
        set
        {
            if (usageFromMenuHeader == value)
                return;
            usageFromMenuHeader = value;
            OnPropertyChanged(nameof(UsageFromMenuHeader));
        }
    }

    /// <summary>
    /// Whether the totals show usage since a date (true) or the current session (false).
    /// Setting it from the context menu asks the owner to change the saved mode, which then calls
    /// <see cref="SetUsageFromTotal"/> to reflect it here.
    /// </summary>
    public bool IsUsageFromTotal
    {
        get => isUsageFromTotal;
        set
        {
            if (isUsageFromTotal == value)
                return;
            selectUsageFromTotal(value);
        }
    }

    public bool IsSessionTotal
    {
        get => !isUsageFromTotal;
        set
        {
            if (isUsageFromTotal != value)
                return;
            selectUsageFromTotal(!value);
        }
    }

    public void SetUsageFromTotal(bool value)
    {
        if (isUsageFromTotal == value)
            return;
        isUsageFromTotal = value;
        OnPropertyChanged(nameof(IsUsageFromTotal));
        OnPropertyChanged(nameof(IsSessionTotal));
    }

    public bool IsPinned
    {
        get => isPinned;
        set
        {
            if (isPinned == value)
                return;
            isPinned = value;
            SettingsManager.Current.MiniWidgetPinned = value;
            SettingsManager.Save();
            OnPropertyChanged(nameof(IsPinned));
            OnPropertyChanged(nameof(PinIconSource));
            OnPropertyChanged(nameof(PinToolTip));
        }
    }

    public string BackgroundColor
    {
        get => backgroundColor;
        private set
        {
            if (backgroundColor == value)
                return;
            backgroundColor = value;
            OnPropertyChanged(nameof(BackgroundColor));
        }
    }

    public IImage PinIconSource => IsPinned
        ? (darkMode ? UnpinDarkImage : UnpinLightImage)
        : (darkMode ? PinDarkImage : PinLightImage);

    public string PinToolTip => IsPinned ? "Unpin" : "Pin";

    public ICommand OpenMainWindowCommand { get; private set; } = new RelayCommand(() => { });
    public ICommand HideWidgetCommand { get; private set; } = new RelayCommand(() => { });
    public ICommand ChooseUsageFromDateCommand { get; private set; } = new RelayCommand(() => { });
    public ICommand TogglePinnedCommand => togglePinnedCommand;

    public MiniWidgetViewModel()
    {
        togglePinnedCommand = new RelayCommand(() => IsPinned = !IsPinned);
        isPinned = SettingsManager.Current.MiniWidgetPinned;
        RefreshBackground(SettingsManager.Current.DarkMode, SettingsManager.Current.MiniWidgetTransparentSlider);
    }

    public void SetActions(Action openMainWindow, Action hideWidget)
    {
        OpenMainWindowCommand = new RelayCommand(openMainWindow);
        HideWidgetCommand = new RelayCommand(hideWidget);
        OnPropertyChanged(nameof(OpenMainWindowCommand));
        OnPropertyChanged(nameof(HideWidgetCommand));
    }

    /// <summary>Wires the context-menu total options to the owner that persists them.</summary>
    public void SetTotalActions(Action<bool> selectUsageFromTotal, Action chooseUsageFromDate)
    {
        this.selectUsageFromTotal = selectUsageFromTotal;
        ChooseUsageFromDateCommand = new RelayCommand(chooseUsageFromDate);
        OnPropertyChanged(nameof(ChooseUsageFromDateCommand));
    }

    public void RefreshBackground(bool darkMode, int transparency)
    {
        var clampedTransparency = Math.Clamp(transparency, 0, 100);
        var alpha = ((100 - clampedTransparency) * 255) / 100;
        this.darkMode = darkMode;
        BackgroundColor = darkMode
            ? $"#{alpha:x2}252525"
            : $"#{alpha:x2}f1f1f1";
        OnPropertyChanged(nameof(PinIconSource));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static Bitmap LoadBitmap(string assetUri)
    {
        try
        {
            using var assetStream = AssetLoader.Open(new Uri(assetUri));
            return new Bitmap(assetStream);
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to load mini widget pin asset '{assetUri}'", ex);
            throw;
        }
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

