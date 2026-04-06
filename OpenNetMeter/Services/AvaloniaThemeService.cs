using Avalonia;
using Avalonia.Styling;

namespace OpenNetMeter.Services;

public sealed class AvaloniaThemeService : IThemeService
{
    private readonly Application application;

    public AvaloniaThemeService(Application application)
    {
        this.application = application;
    }

    public void ApplyDarkMode(bool enabled)
    {
        application.RequestedThemeVariant = enabled
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
    }
}
