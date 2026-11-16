namespace OpenNetMeter.PlatformAbstractions;

public interface IStartupRegistrationService
{
    bool IsEnabled();
    void SetEnabled(bool enabled, bool startMinimized);

    /// <summary>
    /// Re-registers the startup task if it exists but its executable path no longer exists
    /// (e.g. after an upgrade moved the install folder).
    /// </summary>
    void RepairIfStale(bool startMinimized);
}
