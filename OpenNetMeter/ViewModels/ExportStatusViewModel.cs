using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.ViewModels;

/// <summary>
/// Inline feedback for an export: progress, a success message that hides itself after a few seconds
/// (with an "Open folder" action), or a failure that stays until the next export.
/// </summary>
public sealed class ExportStatusViewModel : INotifyPropertyChanged
{
    public static readonly TimeSpan DefaultSuccessDisplayTime = TimeSpan.FromSeconds(5);

    private readonly IFileRevealService fileRevealService;
    private readonly TimeSpan successDisplayTime;
    private readonly RelayCommand revealCommand;
    private CancellationTokenSource? hideCts;
    private string message = string.Empty;
    private bool isError;
    private string? filePath;

    public ExportStatusViewModel(IFileRevealService fileRevealService)
        : this(fileRevealService, DefaultSuccessDisplayTime)
    {
    }

    public ExportStatusViewModel(IFileRevealService fileRevealService, TimeSpan successDisplayTime)
    {
        this.fileRevealService = fileRevealService;
        this.successDisplayTime = successDisplayTime;
        revealCommand = new RelayCommand(Reveal, () => CanReveal);
    }

    public string Message => message;
    public bool IsError => isError;
    public bool IsSuccess => filePath != null && !isError;
    public bool IsVisible => message.Length > 0;
    public string? FilePath => filePath;
    public string FileName => filePath == null ? string.Empty : Path.GetFileName(filePath);
    public bool CanReveal => IsSuccess;
    public ICommand RevealCommand => revealCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ShowProgress() => Set("Exporting...", isError: false, path: null);

    public void ShowSuccess(int rowCount, string path)
    {
        // The file name is shown separately (see FileName) so the view can trim it independently.
        Set($"Exported {rowCount:N0} rows to", isError: false, path);
        HideAfterDelay();
    }

    public void ShowError() => Set("Export failed.", isError: true, path: null);

    public void Clear() => Set(string.Empty, isError: false, path: null);

    private void Reveal()
    {
        if (filePath != null)
            fileRevealService.Reveal(filePath);
    }

    private async void HideAfterDelay()
    {
        var cts = new CancellationTokenSource();
        hideCts = cts;
        try
        {
            await Task.Delay(successDisplayTime, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (ReferenceEquals(hideCts, cts))
            Clear();
    }

    private void Set(string newMessage, bool isError, string? path)
    {
        // Any new state replaces a pending auto-hide.
        hideCts?.Cancel();
        hideCts = null;

        message = newMessage;
        this.isError = isError;
        filePath = path;

        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(CanReveal));
        revealCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
