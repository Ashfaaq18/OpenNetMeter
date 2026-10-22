using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

/// <summary>
/// Read-only summary of a stopped traffic recording, shown in the results dialog, with CSV/JSON export.
/// </summary>
public sealed class RecordingResultsViewModel
{
    private readonly TrafficRecording recording;
    private readonly IFileSaveDialogService fileSaveDialogService;
    private readonly RelayCommand exportCommand;
    private bool isExporting;

    // Internal because TrafficRecording is; the view model itself is public so XAML can bind to it.
    internal RecordingResultsViewModel(
        TrafficRecording recording,
        IProcessIconService processIconService,
        IExternalLinkService externalLinkService,
        IFileSaveDialogService fileSaveDialogService,
        IFileRevealService fileRevealService)
    {
        if (recording.StoppedAt is not { } stoppedAt)
            throw new ArgumentException("The recording must be stopped before its results are shown.", nameof(recording));

        this.recording = recording;
        this.fileSaveDialogService = fileSaveDialogService;
        ExportStatus = new ExportStatusViewModel(fileRevealService);

        StartedText = FormatDateTime(recording.StartedAt);
        EndedText = stoppedAt.Date == recording.StartedAt.Date
            ? stoppedAt.ToString("t", CultureInfo.CurrentCulture)
            : FormatDateTime(stoppedAt);
        DurationText = TrafficRecording.FormatElapsed(recording.GetDuration(stoppedAt));
        AdaptersText = recording.Adapters.Count > 0 ? string.Join(", ", recording.Adapters) : "No network";
        TotalDownloadText = ByteSizeFormatter.FormatBytes(recording.TotalDownload);
        TotalUploadText = ByteSizeFormatter.FormatBytes(recording.TotalUpload);

        Rows = [];
        foreach (var process in recording.GetProcesses())
        {
            var icon = processIconService.GetProcessIcon(process.Process) as IImage;
            Rows.Add(new HistoryRowViewModel(process.Process, process.DownloadBytes, process.UploadBytes, externalLinkService, icon));
        }

        exportCommand = new RelayCommand(async () => await ExportAsync(), () => !isExporting);
    }

    public string StartedText { get; }
    public string EndedText { get; }
    public string DurationText { get; }
    public string AdaptersText { get; }
    public string TotalDownloadText { get; }
    public string TotalUploadText { get; }

    public ObservableCollection<HistoryRowViewModel> Rows { get; }
    public bool HasRows => Rows.Count > 0;

    public ExportStatusViewModel ExportStatus { get; }
    public ICommand ExportCommand => exportCommand;

    private async Task ExportAsync()
    {
        if (isExporting)
            return;

        isExporting = true;
        ExportStatus.Clear();
        exportCommand.RaiseCanExecuteChanged();
        try
        {
            string? path = await fileSaveDialogService.PickSavePathAsync(
                "Export recording",
                $"OpenNetMeter-recording-{recording.StartedAt.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}",
                UsageExporter.FileTypes);
            if (string.IsNullOrWhiteSpace(path))
                return;

            ExportStatus.ShowProgress();
            await RecordingExporter.ExportAsync(recording, path);
            ExportStatus.ShowSuccess(Rows.Count, path);
        }
        catch (Exception ex)
        {
            EventLogger.Error("Error exporting recording", ex);
            ExportStatus.ShowError();
        }
        finally
        {
            isExporting = false;
            exportCommand.RaiseCanExecuteChanged();
        }
    }

    private static string FormatDateTime(DateTime value) =>
        value.ToString("d MMM yyyy", CultureInfo.CurrentCulture) + ", " + value.ToString("t", CultureInfo.CurrentCulture);
}
