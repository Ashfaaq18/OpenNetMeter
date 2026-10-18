using System;
using System.Threading;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Services;

/// <summary>
/// Keeps OpenNetMeter to one running copy per Windows session. A second copy would start its own ETW
/// kernel session under the same name (stopping the first copy's capture) and write to the same database.
/// A later launch hands off to the running copy (asking it to show its window) and exits.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    // Local\ = per sign-in session, so other users on the machine run their own copy.
    private const string DefaultNamePrefix = @"Local\OpenNetMeter";

    private readonly Mutex mutex;
    private readonly EventWaitHandle activateEvent;
    private RegisteredWaitHandle? activationRegistration;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle activateEvent)
    {
        this.mutex = mutex;
        this.activateEvent = activateEvent;
    }

    /// <summary>
    /// Returns a guard if this is the first copy, or null if another copy is already running.
    /// When <paramref name="activateExisting"/> is true, the running copy is asked to show its window.
    /// </summary>
    public static SingleInstanceGuard? TryAcquire(bool activateExisting) =>
        TryAcquire(activateExisting, DefaultNamePrefix);

    /// <summary>Overload with a custom name prefix so tests don't collide with a running copy.</summary>
    internal static SingleInstanceGuard? TryAcquire(bool activateExisting, string namePrefix)
    {
        Mutex? mutex = null;
        EventWaitHandle? activateEvent = null;
        try
        {
            mutex = new Mutex(initiallyOwned: false, namePrefix + ".SingleInstance", out bool createdNew);
            activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, namePrefix + ".Activate");

            if (createdNew)
                return new SingleInstanceGuard(mutex, activateEvent);

            if (activateExisting)
                activateEvent.Set();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            // The objects exist but belong to a copy we can't access, so one is already running.
            EventLogger.Error("Another OpenNetMeter instance is running; could not open its single-instance handles", ex);
        }

        activateEvent?.Dispose();
        mutex?.Dispose();
        return null;
    }

    /// <summary>
    /// Invokes <paramref name="onActivate"/> (on a thread-pool thread) whenever a later launch asks this copy to show itself.
    /// </summary>
    public void ListenForActivation(Action onActivate)
    {
        activationRegistration?.Unregister(null);
        activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            activateEvent, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        activationRegistration?.Unregister(null);
        activateEvent.Dispose();
        mutex.Dispose();
    }
}
