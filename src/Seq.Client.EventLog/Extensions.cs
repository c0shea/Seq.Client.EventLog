using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using Lurgle.Logging;

namespace Seq.Client.EventLog
{
    public static class Extensions
    {
        public static LurgLevel MapLogLevel(EventRecord entry)
        {
            if (entry.Level == null && entry.Keywords == null)
                return LurgLevel.Debug;

            if (entry.Keywords != null)
            {
                if (((StandardEventKeywords)entry.Keywords).HasFlag(StandardEventKeywords.AuditSuccess))
                    return LurgLevel.Error; 
                
                if (((StandardEventKeywords)entry.Keywords).HasFlag(StandardEventKeywords.AuditFailure))
                    return LurgLevel.Warning;
            }

            // ReSharper disable once PossibleInvalidOperationException
            switch ((byte)entry.Level)
            {
                case (byte)EventLogEntryType.Information:
                    return LurgLevel.Information;
                case (byte)EventLogEntryType.Warning:
                    return LurgLevel.Warning;
                case (byte)EventLogEntryType.Error:
                    return LurgLevel.Error;
                case (byte)EventLogEntryType.SuccessAudit:
                    return LurgLevel.Information;
                case (byte)EventLogEntryType.FailureAudit:
                    return LurgLevel.Warning;
                default:
                    return LurgLevel.Debug;
            }
        }

        public static IEnumerable<string> GetArray(string value)
        {
            return (value ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
        }
    }
}