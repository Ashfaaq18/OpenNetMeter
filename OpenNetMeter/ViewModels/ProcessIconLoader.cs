using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.ViewModels;

/// <summary>
/// Gives rows their icon without ever blocking the UI thread: rows start with the cached (or default)
/// icon and <see cref="Attach"/> swaps in the real one once the background load finishes.
/// </summary>
internal static class ProcessIconLoader
{
    public static void Attach(IProcessIconService service, string processName, Action<IImage?> setIcon, Action<Action>? postToUi = null)
    {
        postToUi ??= action => Dispatcher.UIThread.Post(action);

        var load = service.LoadIconAsync(processName);
        if (load.IsCompletedSuccessfully)
        {
            // Already cached: the row was created with this icon, so there is nothing to wait for.
            setIcon(load.Result as IImage);
            return;
        }

        _ = ApplyWhenLoadedAsync(load, processName, setIcon, postToUi);
    }

    private static async Task ApplyWhenLoadedAsync(Task<object?> load, string processName, Action<IImage?> setIcon, Action<Action> postToUi)
    {
        try
        {
            var icon = await load.ConfigureAwait(false);
            // Icon bindings update the visual tree, so the swap has to happen on the UI thread.
            postToUi(() => setIcon(icon as IImage));
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to load icon for process '{processName}'", ex);
        }
    }
}
