namespace OpenNetMeter.Models;

/// <summary>
/// Wording for the warning shown when a pinned adapter is selected but not connected.
/// Pure logic so it can be unit tested without the view models.
/// </summary>
internal static class PinnedAdapterStatus
{
    private const string FallbackAdapterName = "the selected adapter";

    /// <summary>
    /// Returns the warning text, or an empty string when there is nothing to warn about:
    /// automatic selection (no pinned adapter) or the network is connected.
    /// </summary>
    public static string GetWarningText(string? pinnedAdapterId, string? pinnedAdapterName, bool isConnected)
    {
        if (string.IsNullOrEmpty(pinnedAdapterId) || isConnected)
            return string.Empty;

        var name = string.IsNullOrWhiteSpace(pinnedAdapterName) ? FallbackAdapterName : pinnedAdapterName;
        return $"Pinned adapter \"{name}\" isn't connected \u2014 nothing is being counted";
    }
}
