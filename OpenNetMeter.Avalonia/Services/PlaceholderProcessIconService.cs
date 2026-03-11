using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Avalonia.Services;

public sealed class PlaceholderProcessIconService : IProcessIconService
{
    public object? GetProcessIcon(string processName) => null;
}
