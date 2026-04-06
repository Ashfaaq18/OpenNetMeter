namespace OpenNetMeter.PlatformAbstractions;

public interface IStartupRegistrationService
{
    bool IsEnabled();
    void SetEnabled(bool enabled, bool startMinimized);
}
