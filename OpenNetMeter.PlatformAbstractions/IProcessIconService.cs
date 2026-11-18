using System.Threading.Tasks;

namespace OpenNetMeter.PlatformAbstractions;

public interface IProcessIconService
{
    /// <summary>
    /// The cached icon for <paramref name="processName"/>, or the default icon if it has not been loaded yet.
    /// Never blocks, so it is safe on the UI thread.
    /// </summary>
    object? GetCachedIcon(string processName);

    /// <summary>
    /// Loads the icon on a background thread and caches it (the default icon if the executable can't be found).
    /// Concurrent requests for the same process share one load, and loads run one at a time.
    /// </summary>
    Task<object?> LoadIconAsync(string processName);
}
