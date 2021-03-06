﻿using System.Collections.Generic;
using System.Diagnostics;

namespace Seq.Client.EventLog
{
    public static class Extensions
    {
        private static string MapLogLevel(EventLogEntryType type)
        {
            switch (type)
            {
                case EventLogEntryType.Information:
                    return "Information";
                case EventLogEntryType.Warning:
                    return "Warning";
                case EventLogEntryType.Error:
                    return "Error";
                case EventLogEntryType.SuccessAudit:
                    return "Information";
                case EventLogEntryType.FailureAudit:
                    return "Warning";
                default:
                    return "Debug";
            }
        }

        public static RawEvents ToDto(this EventLogEntry entry, string logName)
        {
            return new RawEvents
            {
                Events = new[]
                {
                    new RawEvent
                    {
                        Timestamp = entry.TimeGenerated,
                        Level = MapLogLevel(entry.EntryType),
                        MessageTemplate = entry.Message,
                        Properties = new Dictionary<string, object>
                        {
                            { "MachineName", entry.MachineName },
#pragma warning disable 618
                            { "EventId", entry.EventID },
#pragma warning restore 618
                            { "InstanceId", entry.InstanceId },
                            { "Source", entry.Source },
                            { "Category", entry.CategoryNumber },
                            { "EventLogName", logName }
                        }
                    },
                }
            };
        }
    }
}
