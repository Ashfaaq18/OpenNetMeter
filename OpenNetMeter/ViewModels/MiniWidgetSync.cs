using System;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

/// <summary>Everything the mini widget shows, so one sync is one pass of assignments.</summary>
internal readonly record struct MiniWidgetTexts(
    string DownloadSpeedText,
    string UploadSpeedText,
    string UsageFromMenuHeader,
    string TotalLabel,
    string TotalToolTip,
    string TotalDownloadText,
    string TotalUploadText);

/// <summary>
/// Keeps the mini widget's texts in step with the Summary view model, once per flush. The widget is on screen
/// whenever the app runs, so unlike the Summary page's own bindings it must not depend on the main window being
/// visible: the Summary holds back its property notifications while the window is hidden, and this follows the
/// per-flush <see cref="SummaryViewModel.Flushed"/> event instead (plus <see cref="SummaryViewModel.WidgetStateChanged"/>
/// for the changes that happen between flushes).
/// </summary>
internal sealed class MiniWidgetSync : IDisposable
{
    private readonly SummaryViewModel summary;
    private readonly Func<bool> isUsageFromTotal;
    private readonly Action<MiniWidgetTexts> apply;

    public MiniWidgetSync(SummaryViewModel summary, Func<bool> isUsageFromTotal, Action<MiniWidgetTexts> apply)
    {
        this.summary = summary;
        this.isUsageFromTotal = isUsageFromTotal;
        this.apply = apply;
        summary.Flushed += OnSummaryChanged;
        summary.WidgetStateChanged += OnSummaryChanged;
    }

    public void Dispose()
    {
        summary.Flushed -= OnSummaryChanged;
        summary.WidgetStateChanged -= OnSummaryChanged;
    }

    /// <summary>Reads the Summary's current values and hands them to the widget.</summary>
    public void Sync()
    {
        var fromDate = summary.WidgetUsageFromDate;
        var fromToday = fromDate == DateTime.Today;
        var fromDateText = DisplayDateFormat.Date(fromDate);
        var menuHeader = fromToday ? "Usage from today" : $"Usage from {fromDateText}";

        if (isUsageFromTotal())
        {
            apply(new MiniWidgetTexts(
                summary.DownloadSpeedText,
                summary.UploadSpeedText,
                menuHeader,
                fromToday ? "Today:" : "Since:",
                fromToday ? "Usage today, since midnight" : $"Usage since {fromDateText}",
                summary.WidgetUsageDownloadText,
                summary.WidgetUsageUploadText));
            return;
        }

        apply(new MiniWidgetTexts(
            summary.DownloadSpeedText,
            summary.UploadSpeedText,
            menuHeader,
            "Total:",
            "Current session",
            summary.CurrentSessionDownloadText,
            summary.CurrentSessionUploadText));
    }

    private void OnSummaryChanged(object? sender, EventArgs e) => Sync();
}
