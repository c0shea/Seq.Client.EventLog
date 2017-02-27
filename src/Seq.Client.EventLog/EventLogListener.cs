using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Seq.Client.EventLog.Properties;

namespace Seq.Client.EventLog
{
    public class EventLogListener
    {
        public string LogName { get; set; }
        public string MachineName { get; set; }
        public string Source { get; set; }

        // These two properties allow for the filterting of events that will be sent to Seq.
        // If they are not specified in the JSON, all events in the log will be sent.
        public List<EventLogEntryType> LogLevels { get; set; }
        public List<int> EventIds { get; set; }

        private System.Diagnostics.EventLog _eventLog;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LogName))
            {
                throw new InvalidOperationException("A LogName must be specified for the listener.");
            }
        }

        public void Start()
        {
            try
            {
                _eventLog = new System.Diagnostics.EventLog(LogName);

                if (!string.IsNullOrWhiteSpace(MachineName))
                {
                    _eventLog.MachineName = MachineName;
                }

                if (!string.IsNullOrWhiteSpace(Source))
                {
                    _eventLog.Source = Source;
                }

                _eventLog.EntryWritten += EventLogEntryWritten;
                _eventLog.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
                
            }
        }

        public void Stop()
        {
            try
            {
                _eventLog.EnableRaisingEvents = false;
                _eventLog.Close();
                _eventLog.Dispose();
            }
            catch (Exception ex)
            {
                
            }
        }

        private void EventLogEntryWritten(object sender, EntryWrittenEventArgs e)
        {
            var entry = e.Entry;

            if (LogLevels != null && LogLevels.Count > 0 && !LogLevels.Contains(entry.EntryType))
                return;

            if (EventIds != null && EventIds.Count > 0 && !EventIds.Contains(entry.EventID))
                return;

            var rawEvent = new RawEvents
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
                            { "EventId", entry.EventID },
                            { "InstanceId", entry.InstanceId },
                            { "Source", entry.Source },
                            { "Category", entry.Category }
                        }
                    },
                }
            };

            PostRawEvents(rawEvent);
        }

        private static void PostRawEvents(RawEvents rawEvents)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var uri = Settings.Default.SeqUri + "/api/events/raw";

                    if (!string.IsNullOrWhiteSpace(Settings.Default.ApiKey))
                    {
                        uri += "?apiKey=" + Settings.Default.ApiKey;
                    }

                    var content = new StringContent(JsonConvert.SerializeObject(rawEvents, Formatting.None),
                        Encoding.UTF8, "application/json");
                    var result = client.PostAsync(uri, content).Result;
                }
            }
            catch (Exception)
            {

            }
        }

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
    }
}
