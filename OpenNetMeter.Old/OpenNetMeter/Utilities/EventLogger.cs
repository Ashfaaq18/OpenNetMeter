using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace OpenNetMeter.Utilities
{
    public static class EventLogger
    {
        private const string EventSourceName = "OpenNetMeter";
        private const int MaxEventLogMessageLength = 31839;

        public static void Info(string message, int eventId = 1000, short category = 0)
        {
            WriteEntrySafe(message, EventLogEntryType.Information, eventId, category);
        }

        public static void Warn(string message, int eventId = 2000, short category = 0)
        {
            WriteEntrySafe(message, EventLogEntryType.Warning, eventId, category);
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
            WriteEntrySafe(logMessage, EventLogEntryType.Error, eventId, category);
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
            WriteEntrySafe(logMessage, EventLogEntryType.Error, eventId, category);
        }

        private static string AddCallerContext(string message, string memberName, string filePath, int lineNumber)
        {
            string fileName = string.IsNullOrWhiteSpace(filePath) ? "unknown" : Path.GetFileName(filePath);
            return $"[{fileName}:{lineNumber} in {memberName}] {message}";
        }

        private static void WriteEntrySafe(string message, EventLogEntryType entryType, int eventId, short category)
        {
            string safeMessage = Truncate(message);
            Debug.WriteLine(safeMessage);

            try
            {
                EventLog.WriteEntry(EventSourceName, safeMessage, entryType, eventId, category);
            }
            catch (Exception writeEx)
            {
                Debug.WriteLine($"[EventLogger fallback] Failed to write to Windows Event Log: {writeEx}");
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
}
