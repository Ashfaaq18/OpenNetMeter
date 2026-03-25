namespace OpenNetMeter.Properties;

public class AppSettings
{
    public bool DarkMode { get; set; }
    public bool StartWithWin { get; set; }
    public bool MinimizeOnStart { get; set; } = true;
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
