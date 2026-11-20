using System;
using System.Collections.Generic;

namespace OpenNetMeter.Views;

/// <summary>What the pre-warm sequence needs from the main window; the window implements it, tests fake it.</summary>
internal interface IPreWarmHost
{
    /// <summary>The window was shown to the user at some point (or is showing now).</summary>
    bool HasBeenShown { get; }

    bool IsVisible { get; }

    /// <summary>Starts ignoring open/position/size events and stops the window taking focus or a taskbar button.</summary>
    void BeginWarmUp();

    /// <summary>Makes the window invisible while it is shown (DWM cloak). False if that isn't possible.</summary>
    bool TryCloak();

    /// <summary>Moves the window outside every screen, for when it can't be cloaked. False if that isn't possible.</summary>
    bool TryMoveOffScreen();

    void Show();

    /// <summary>Calls <paramref name="then"/> after <paramref name="count"/> frames have been rendered.</summary>
    void WaitForFrames(int count, Action then);

    /// <summary>Calls <paramref name="then"/> after <paramref name="delay"/>.</summary>
    void ScheduleTimeout(TimeSpan delay, Action then);

    void Hide();
    void Uncloak();
    void RestorePosition();

    /// <summary>Restores the focus/taskbar behaviour and starts handling window events again.</summary>
    void EndWarmUp();

    /// <summary>The warm-up finished normally (not skipped or failed).</summary>
    void Completed();

    /// <summary>Opens the window the way the tray icon does.</summary>
    void ShowNormally();

    void LogFailure(string message, Exception ex);
}

/// <summary>
/// Pays the main window's first-show cost (cold templates, layout, the GPU surface, first-run code) while the user
/// isn't looking, so their first open of the window is as quick as the later ones. A /StartMinimized launch never
/// shows the window, so a few seconds after startup it is shown invisibly for a couple of frames and hidden again.
///
/// The sequence: begin (flags on), cloak (or move off-screen if the DWM refuses), show, wait for two rendered frames,
/// hide, uncloak, restore. If the user opens the window while that runs, the open is held until the sequence has
/// finished and then done normally. The restore always runs, even if a step throws, so a window is never left
/// invisible or off-screen.
/// </summary>
internal sealed class MainWindowPreWarm
{
    /// <summary>Frames to wait for: the first lays the window out, the second presents what the first drew.</summary>
    internal const int FramesToWait = 2;

    /// <summary>The frames normally arrive within a few tens of milliseconds; this is the safety net.</summary>
    internal static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(2);

    private readonly IPreWarmHost host;
    private readonly List<Action> afterWarmUp = [];
    private bool attempted;
    private bool cloaked;
    private bool movedOffScreen;
    private bool showed;

    public MainWindowPreWarm(IPreWarmHost host)
    {
        this.host = host;
    }

    /// <summary>True from the start of the sequence until the window has been hidden and restored.</summary>
    public bool IsWarmingUp { get; private set; }

    /// <summary>
    /// Starts the pre-warm if it is wanted: only for a minimized launch, only once, and only if the user hasn't opened
    /// the window already. Returns whether a pre-warm began.
    /// </summary>
    public bool TryStart(bool startMinimized)
    {
        if (!startMinimized || attempted || IsWarmingUp || host.HasBeenShown || host.IsVisible)
            return false;

        attempted = true;
        IsWarmingUp = true;

        try
        {
            host.BeginWarmUp();

            cloaked = host.TryCloak();
            if (!cloaked)
            {
                movedOffScreen = host.TryMoveOffScreen();
                if (!movedOffScreen)
                {
                    // Can't hide it, so don't show it.
                    Finish(completed: false);
                    return false;
                }
            }

            showed = true;
            host.Show();
            host.WaitForFrames(FramesToWait, () => Finish(completed: true));
            host.ScheduleTimeout(MaxWait, () => Finish(completed: true));
            return true;
        }
        catch (Exception ex)
        {
            host.LogFailure("Failed to pre-warm the main window", ex);
            Finish(completed: false);
            return false;
        }
    }

    /// <summary>
    /// Opens the window for the user. While the pre-warm runs the open is held until it has finished (a frame or two)
    /// and then done normally; otherwise it is done right away.
    /// </summary>
    public void OpenForUser() => RunWhenIdle(host.ShowNormally);

    /// <summary>Runs <paramref name="action"/> now, or once the pre-warm has finished if one is running.</summary>
    public void RunWhenIdle(Action action)
    {
        if (IsWarmingUp)
            afterWarmUp.Add(action);
        else
            action();
    }

    // Called by whichever of the frame wait and the timeout comes first; the other finds it already done.
    private void Finish(bool completed)
    {
        if (!IsWarmingUp)
            return;

        try
        {
            if (showed)
                host.Hide();
        }
        catch (Exception ex)
        {
            host.LogFailure("Failed to hide the pre-warmed main window", ex);
        }
        finally
        {
            Restore();
        }

        if (completed)
            host.Completed();

        var pending = afterWarmUp.ToArray();
        afterWarmUp.Clear();
        foreach (var action in pending)
            action();
    }

    private void Restore()
    {
        try
        {
            if (cloaked)
                host.Uncloak();
        }
        catch (Exception ex)
        {
            host.LogFailure("Failed to uncloak the pre-warmed main window", ex);
        }

        try
        {
            if (movedOffScreen)
                host.RestorePosition();
        }
        catch (Exception ex)
        {
            host.LogFailure("Failed to restore the pre-warmed main window's position", ex);
        }
        finally
        {
            host.EndWarmUp();
            IsWarmingUp = false;
        }
    }
}
