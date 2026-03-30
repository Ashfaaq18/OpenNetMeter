using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia.Services;

public sealed class PlaceholderStartupRegistrationService : IStartupRegistrationService
{
    public bool IsEnabled() => false;

    public void SetEnabled(bool enabled, bool startMinimized)
    {
    }
}
