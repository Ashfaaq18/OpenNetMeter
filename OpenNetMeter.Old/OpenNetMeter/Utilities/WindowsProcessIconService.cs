using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Utilities
{
    public sealed class WindowsProcessIconService : IProcessIconService
    {
        public object? GetProcessIcon(string processName)
        {
            return ProcessIconCache.GetIcon(processName);
        }
    }
}
