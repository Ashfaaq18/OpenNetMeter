namespace OpenNetMeter.ViewModels;

/// <summary>
/// Works out what the Summary view model should be told about what is on screen. The Summary page's values are only
/// shown while the main window is visible (not hidden to the tray, not minimized) and the Summary tab is selected,
/// but the status bar's speed texts are shown on every tab, so those follow the window alone.
/// </summary>
internal sealed class SummaryActivityGate
{
    private readonly SummaryViewModel summary;
    private bool windowActive;
    private bool summaryTabSelected = true;

    /// <summary>Starts with the window not showing: the app usually starts minimized to the tray.</summary>
    public SummaryActivityGate(SummaryViewModel summary)
    {
        this.summary = summary;
        Apply();
    }

    /// <summary>The main window is visible and not minimized.</summary>
    public void SetWindowActive(bool active)
    {
        windowActive = active;
        Apply();
    }

    public void SetSummaryTabSelected(bool selected)
    {
        summaryTabSelected = selected;
        Apply();
    }

    // The view model ignores a value it already has, so this can run on every change.
    private void Apply()
    {
        summary.SetUiActive(windowActive && summaryTabSelected);
        summary.SetSpeedActive(windowActive);
    }
}
