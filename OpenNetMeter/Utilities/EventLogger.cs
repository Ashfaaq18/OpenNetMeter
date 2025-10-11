using System;
using System.Diagnostics;

namespace OpenNetMeter.Utilities
{
    public static class EventLogger
    {
        private const string EventSourceName = "OpenNetMeter";
        private const string LogName = "Application";

        public static void Info(string message, int eventId = 1000)
            => EventLog.WriteEntry(EventSourceName, message, EventLogEntryType.Information, eventId);

        public static void Warn(string message, int eventId = 2000)
            => EventLog.WriteEntry(EventSourceName, message, EventLogEntryType.Warning, eventId);

        public static void Error(string message, int eventId = 3000, short category = 0)
            => EventLog.WriteEntry(EventSourceName, message, EventLogEntryType.Error, eventId);

        public static void Error(string message, Exception ex, int eventId = 3001)
        {
            string errorMessage = $"{message}{Environment.NewLine}{ex}";
            EventLog.WriteEntry(EventSourceName, errorMessage, EventLogEntryType.Error, eventId);
        }
    }
}
