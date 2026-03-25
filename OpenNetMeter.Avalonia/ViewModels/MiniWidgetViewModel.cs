using System.ComponentModel;

namespace OpenNetMeter.Avalonia.ViewModels;

public sealed class MiniWidgetViewModel : INotifyPropertyChanged
{
    private string downloadSpeedText = "35.2 Mbps";
    private string uploadSpeedText = "4.8 Mbps";
    private string currentSessionDownloadText = "1.24 GB";
    private string currentSessionUploadText = "98.4 MB";

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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
