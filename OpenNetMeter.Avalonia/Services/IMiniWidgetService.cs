namespace OpenNetMeter.Avalonia.Services;

public interface IMiniWidgetService : System.IDisposable
{
    event System.Action<bool>? VisibilityChanged;

    void Show();
    void Hide();
    void RefreshAppearance(bool darkMode, int transparency);
}
