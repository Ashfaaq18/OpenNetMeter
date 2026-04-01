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
    private string currentSessionDownloadText = "1.24 GB";
    private string currentSessionUploadText = "98.4 MB";
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

    public string CurrentSessionDownloadText
    {
        get => currentSessionDownloadText;
        set
        {
            if (currentSessionDownloadText == value)
                return;
            currentSessionDownloadText = value;
            OnPropertyChanged(nameof(CurrentSessionDownloadText));
        }
    }

    public string CurrentSessionUploadText
    {
        get => currentSessionUploadText;
        set
        {
            if (currentSessionUploadText == value)
                return;
            currentSessionUploadText = value;
            OnPropertyChanged(nameof(CurrentSessionUploadText));
        }
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

