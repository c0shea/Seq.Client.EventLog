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
        public List<string> Sources { get; set; }
        public bool ProcessRetroactiveEntries { get; set; }
        
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
                
                _eventLog.EntryWritten += (sender, args) =>
                {
                    HandleEventLogEntry(args.Entry);
                };
                _eventLog.EnableRaisingEvents = true;

                if (ProcessRetroactiveEntries)
                {
                    HandleRetroactiveEntries();
                }
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

        private void HandleRetroactiveEntries()
        {
            foreach (EventLogEntry entry in _eventLog.Entries)
            {
                HandleEventLogEntry(entry);
            }
        }

        private void HandleEventLogEntry(EventLogEntry entry)
        {
            // Don't send the entry to Seq if it doesn't match the filtered log levels, event IDs, or sources
            if (LogLevels != null && LogLevels.Count > 0 && !LogLevels.Contains(entry.EntryType))
                return;

            if (EventIds != null && EventIds.Count > 0 && !EventIds.Contains(entry.EventID))
                return;

            if (Sources != null && Sources.Count > 0 && !Sources.Contains(entry.Source))
                return;

            PostRawEvents(entry.ToDto());
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
    }
}
