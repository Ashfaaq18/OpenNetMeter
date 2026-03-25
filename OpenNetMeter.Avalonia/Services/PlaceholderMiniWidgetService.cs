namespace OpenNetMeter.Avalonia.Services;

public sealed class PlaceholderMiniWidgetService : IMiniWidgetService
{
    public event System.Action<bool>? VisibilityChanged
    {
        add { }
        remove { }
    }

    public void Show()
    {
    }

    public void Hide()
    {
    }

    public void RefreshAppearance(bool darkMode, int transparency)
    {
    }

    public void Dispose()
    {
    }
}
