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
    public int MiniWidgetTransparentSlider { get; set; } = 20;

    public int NetworkType { get; set; } = 2;
    public int NetworkSpeedFormat { get; set; } = 0;
    public int NetworkSpeedMagnitude { get; set; } = 0;
}
