using System.Collections.Generic;
using System.Diagnostics;
using Lurgle.Logging;

namespace Seq.Client.EventLog
{
    public static class Extensions
    {
        public static LurgLevel MapLogLevel(byte? type)
        {
            switch (type)
            {
                case (byte?)EventLogEntryType.Information:
                    return LurgLevel.Information;
                case (byte?)EventLogEntryType.Warning:
                    return LurgLevel.Warning;
                case (byte?)EventLogEntryType.Error:
                    return LurgLevel.Error;
                case (byte?)EventLogEntryType.SuccessAudit:
                    return LurgLevel.Information;
                case (byte?)EventLogEntryType.FailureAudit:
                    return LurgLevel.Warning;
                default:
                    return LurgLevel.Debug;
            }
        }

//        public static RawEvents ToDto(this EventLogEntry entry, string logName)
//        {
//            return new RawEvents
//            {
//                Events = new[]
//                {
//                    new RawEvent
//                    {
//                        Timestamp = entry.TimeGenerated,
//                        Level = MapLogLevel(entry.EntryType),
//                        MessageTemplate = entry.Message,
//                        Properties = new Dictionary<string, object>
//                        {
//                            { "MachineName", entry.MachineName },
//#pragma warning disable 618
//                            { "EventId", entry.EventID },
//#pragma warning restore 618
//                            { "InstanceId", entry.InstanceId },
//                            { "Source", entry.Source },
//                            { "Category", entry.CategoryNumber },
//                            { "EventLogName", logName }
//                        }
//                    },
//                }
//            };
//        }
    }
}
