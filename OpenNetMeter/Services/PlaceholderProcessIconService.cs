using System.Threading.Tasks;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Services;

public sealed class PlaceholderProcessIconService : IProcessIconService
{
    public object? GetCachedIcon(string processName) => null;

    public Task<object?> LoadIconAsync(string processName) => Task.FromResult<object?>(null);
}

