using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Services;

public sealed class PlaceholderStartupRegistrationService : IStartupRegistrationService
{
    public bool IsEnabled() => false;

    public void SetEnabled(bool enabled, bool startMinimized)
    {
    }
}

