namespace OpenNetMeter.Properties;

public class AppSettings
{
    public bool DarkMode { get; set; }
    public bool StartWithWin { get; set; }
    public bool MinimizeOnStart { get; set; } = true;
    public int WinPosX { get; set; }
    public int WinPosY { get; set; }
    public int WinWidth { get; set; } = 900;
    public int WinHeight { get; set; } = 600;
    public bool MainWindowPositionInitialized { get; set; }
    public bool MiniWidgetVisibility { get; set; } = true;
    public bool MiniWidgetPinned { get; set; }
    public int MiniWidgetPosX { get; set; }
    public int MiniWidgetPosY { get; set; }
    public bool MiniWidgetPositionInitialized { get; set; }
    public int MiniWidgetTransparentSlider { get; set; } = 0;

    public int NetworkType { get; set; } = 2;
    public int NetworkSpeedFormat { get; set; } = 0;
    public int NetworkSpeedMagnitude { get; set; } = 0;

    // Pinned network adapter Id; empty = automatic (the adapter Windows routes internet traffic through).
    public string NetworkAdapterId { get; set; } = string.Empty;
    // Last known display name of the pinned adapter, used to label it while it is disconnected.
    public string NetworkAdapterName { get; set; } = string.Empty;

    public int HistoryRetentionDays { get; set; } = Models.HistoryRetention.DefaultDays;
}
