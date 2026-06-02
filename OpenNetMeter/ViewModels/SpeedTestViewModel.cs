using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
using OpenNetMeter.Services;

namespace OpenNetMeter.ViewModels;

public sealed class SpeedTestViewModel : INotifyPropertyChanged
{
    private readonly ISpeedTestService speedTestService;
    private readonly RelayCommand runTestCommand;
    private CancellationTokenSource? cts;

    private TestPhase currentPhase = TestPhase.Idle;
    private double liveSpeedMbps;
    private double? pingMs;
    private double? downloadMbps;
    private double? uploadMbps;
    private string statusText = "Ready";

    public SpeedTestViewModel(ISpeedTestService speedTestService)
    {
        this.speedTestService = speedTestService;
        runTestCommand = new RelayCommand(StartTest, () => !IsTesting);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand RunTestCommand => runTestCommand;

    public bool IsTesting => currentPhase is not (TestPhase.Idle or TestPhase.Done or TestPhase.Failed);

    public bool IsDownloadPhase => currentPhase == TestPhase.Downloading;
    public bool IsUploadPhase => currentPhase == TestPhase.Uploading;

    public string StatusText
    {
        get => statusText;
        private set
        {
            if (statusText == value) return;
            statusText = value;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public double LiveSpeedMbps
    {
        get => liveSpeedMbps;
        private set
        {
            if (Math.Abs(liveSpeedMbps - value) < 0.001) return;
            liveSpeedMbps = value;
            OnPropertyChanged(nameof(LiveSpeedMbps));
            OnPropertyChanged(nameof(LiveSpeedText));
        }
    }

    public string LiveSpeedText => liveSpeedMbps > 0 ? $"{liveSpeedMbps:F1} Mbps" : "---";
    public string PingText => pingMs.HasValue ? $"{pingMs.Value:F0} ms" : "---";
    public string DownloadText => downloadMbps.HasValue ? $"{downloadMbps.Value:F1} Mbps" : "---";
    public string UploadText => uploadMbps.HasValue ? $"{uploadMbps.Value:F1} Mbps" : "---";
    public string RunButtonText => IsTesting ? "Running..." : "Start Test";

    private async void StartTest()
    {
        if (IsTesting) return;

        pingMs = null;
        downloadMbps = null;
        uploadMbps = null;
        LiveSpeedMbps = 0;

        cts = new CancellationTokenSource();
        SetPhase(TestPhase.Pinging);

        var progress = new Progress<SpeedTestProgress>(OnProgress);
        await speedTestService.RunAsync(progress, cts.Token);
    }

    private void OnProgress(SpeedTestProgress p)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (p.PingMs.HasValue) pingMs = p.PingMs;
            if (p.DownloadMbps.HasValue) downloadMbps = p.DownloadMbps;
            if (p.UploadMbps.HasValue) uploadMbps = p.UploadMbps;

            if (p.Phase is TestPhase.Done or TestPhase.Failed or TestPhase.Idle)
                LiveSpeedMbps = 0;
            else if (p.LiveSpeedMbps > 0)
                LiveSpeedMbps = p.LiveSpeedMbps;

            SetPhase(p.Phase, p.ErrorMessage);
        });
    }

    private void SetPhase(TestPhase phase, string? errorMessage = null)
    {
        currentPhase = phase;

        StatusText = phase switch
        {
            TestPhase.Idle => "Ready",
            TestPhase.Pinging => "Measuring latency...",
            TestPhase.Downloading => "Downloading...",
            TestPhase.Uploading => "Uploading...",
            TestPhase.Done => "Done",
            TestPhase.Failed => $"Failed: {errorMessage ?? "unknown error"}",
            _ => "Ready"
        };

        OnPropertyChanged(nameof(IsTesting));
        OnPropertyChanged(nameof(IsDownloadPhase));
        OnPropertyChanged(nameof(IsUploadPhase));
        OnPropertyChanged(nameof(PingText));
        OnPropertyChanged(nameof(DownloadText));
        OnPropertyChanged(nameof(UploadText));
        OnPropertyChanged(nameof(LiveSpeedText));
        OnPropertyChanged(nameof(RunButtonText));

        runTestCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
