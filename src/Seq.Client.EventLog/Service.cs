using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.ServiceProcess;
using System.Text;
using Newtonsoft.Json;
using Seq.Client.EventLog.Properties;

namespace Seq.Client.EventLog
{
    public partial class Service : ServiceBase
    {
        private System.Diagnostics.EventLog _eventLog;

        #region Windows Service Base
        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _eventLog = new System.Diagnostics.EventLog("Application");
            _eventLog.EntryWritten += EventLogOnEntryWritten;
            _eventLog.EnableRaisingEvents = true;
        }

        protected override void OnStop()
        {
            _eventLog.EnableRaisingEvents = false;
            _eventLog.Close();
            _eventLog.Dispose();
        }
        #endregion

        private void EventLogOnEntryWritten(object sender, EntryWrittenEventArgs entryWrittenEventArgs)
        {
            var entry = entryWrittenEventArgs.Entry;

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

        private void PostRawEvents(RawEvents rawEvents)
        {
            using (var client = new HttpClient())
            {
                var uri = Settings.Default.SeqUri + "/api/events/raw";

                if (!string.IsNullOrWhiteSpace(Settings.Default.ApiKey))
                {
                    uri += "?apiKey=" + Settings.Default.ApiKey;
                }

                var content = new StringContent(JsonConvert.SerializeObject(rawEvents, Formatting.None), Encoding.UTF8, "application/json");
                var result = client.PostAsync(uri, content).Result;
            }
        }
    }
}
