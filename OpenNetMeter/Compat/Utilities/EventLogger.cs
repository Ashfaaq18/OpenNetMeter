using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace OpenNetMeter.Utilities;

public static class EventLogger
{
    private const string EventSourceName = "OpenNetMeter";
    private const int MaxEventLogMessageLength = 31839;

    // Far more than a burst of errors produces; only a stuck Event Log can fill it, and then dropping beats blocking.
    private const int MaxQueuedEntries = 1024;

    private enum LogLevel
    {
        Information,
        Warning,
        Error
    }

    private readonly record struct LogEntry(string Message, LogLevel Level, int EventId, short Category);

    // Created on the first Windows write (so nothing starts for a process that never logs).
    private static readonly object writeQueueLock = new();
    private static volatile BackgroundWriteQueue<LogEntry>? writeQueue;

    public static void Info(string message, int eventId = 1000, short category = 0)
    {
        WriteEntrySafe(message, LogLevel.Information, eventId, category);
    }

    public static void Warn(string message, int eventId = 2000, short category = 0)
    {
        WriteEntrySafe(message, LogLevel.Warning, eventId, category);
    }

    public static void Error(
        string message,
        int eventId = 3000,
        short category = 0,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string logMessage = AddCallerContext(message, memberName, filePath, lineNumber);
        WriteEntrySafe(logMessage, LogLevel.Error, eventId, category);
    }

    public static void Error(
        Exception ex,
        int eventId = 3001,
        short category = 0,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        Error("Unhandled exception", ex, eventId, category, memberName, filePath, lineNumber);
    }

    public static void Error(
        string message,
        Exception ex,
        int eventId = 3001,
        short category = 0,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string exceptionText = ex is AggregateException aggregateEx
            ? aggregateEx.Flatten().ToString()
            : ex.ToString();
        string errorMessage = $"{message}{Environment.NewLine}{exceptionText}";
        string logMessage = AddCallerContext(errorMessage, memberName, filePath, lineNumber);
        WriteEntrySafe(logMessage, LogLevel.Error, eventId, category);
    }

    private static string AddCallerContext(string message, string memberName, string filePath, int lineNumber)
    {
        string fileName = string.IsNullOrWhiteSpace(filePath) ? "unknown" : Path.GetFileName(filePath);
        return $"[{fileName}:{lineNumber} in {memberName}] {message}";
    }

    /// <summary>
    /// Waits (up to <paramref name="timeout"/>) for queued Event Log entries to be written. Entries are written on a
    /// background thread, so call this before the process exits or it may drop the last few. Returns false on timeout.
    /// </summary>
    public static bool Flush(TimeSpan timeout)
    {
        var queue = writeQueue;
        return queue == null || queue.Flush(timeout);
    }

    private static void WriteEntrySafe(string message, LogLevel level, int eventId, short category)
    {
        string safeMessage = Truncate(message);
        Debug.WriteLine(safeMessage);

        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            // Never blocks: the Event Log write happens on the writer thread.
            if (!GetWriteQueue().TryEnqueue(new LogEntry(safeMessage, level, eventId, category)))
                Debug.WriteLine("[EventLogger fallback] Event Log queue is full; entry was not written to the Event Log.");
        }
        catch (Exception writeEx)
        {
            Debug.WriteLine($"[EventLogger fallback] Failed to queue Windows Event Log entry: {writeEx}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static BackgroundWriteQueue<LogEntry> GetWriteQueue()
    {
        var queue = writeQueue;
        if (queue != null)
            return queue;

        lock (writeQueueLock)
        {
            return writeQueue ??= new BackgroundWriteQueue<LogEntry>(new EventLogSink().Write, MaxQueuedEntries, "EventLogger");
        }
    }

    /// <summary>
    /// Runs on the writer thread only. Holds one <see cref="EventLog"/> for its lifetime: the static
    /// <c>EventLog.WriteEntry(source, ...)</c> re-checks the source in the registry on every call, whereas an
    /// instance verifies (and, as before, registers a missing source) once on its first write.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private sealed class EventLogSink
    {
        private EventLog? eventLog;

        public void Write(LogEntry entry)
        {
            try
            {
                var entryType = entry.Level switch
                {
                    LogLevel.Information => EventLogEntryType.Information,
                    LogLevel.Warning => EventLogEntryType.Warning,
                    _ => EventLogEntryType.Error
                };

                eventLog ??= new EventLog { Source = EventSourceName };
                eventLog.WriteEntry(entry.Message, entryType, entry.EventId, entry.Category);
            }
            catch (Exception writeEx)
            {
                // Start over with a fresh instance (and source check) on the next entry.
                eventLog?.Dispose();
                eventLog = null;
                Debug.WriteLine($"[EventLogger fallback] Failed to write to Windows Event Log: {writeEx}");
            }
        }
    }

    private static string Truncate(string message)
    {
        if (message.Length <= MaxEventLogMessageLength)
            return message;

        const string suffix = "... [truncated]";
        int maxLength = MaxEventLogMessageLength - suffix.Length;
        return message[..Math.Max(0, maxLength)] + suffix;
    }
}
