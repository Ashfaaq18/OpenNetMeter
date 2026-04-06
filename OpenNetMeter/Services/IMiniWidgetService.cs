using Avalonia.Controls;

namespace OpenNetMeter.Services;

public interface IMiniWidgetService : System.IDisposable
{
    event System.Action<bool>? VisibilityChanged;

    void Show();
    void Hide();
    void RefreshAppearance(bool darkMode, int transparency);
    void ResetPosition(Window mainWindow);
    void EnsurePositionOnScreen(Window mainWindow);
}

