using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Lurgle.Logging;

namespace Seq.Client.EventLog
{
    public class EventLogListener
    {
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private EventLogQuery _eventLog;
        private bool _isInteractive;
        private volatile bool _started;
        private EventLogWatcher _watcher;

        public string LogName { get; set; }
        public string MachineName { get; set; }

        // These properties allow for the filtering of events that will be sent to Seq.
        // If they are not specified in the JSON, all events in the log will be sent.
        public List<byte> LogLevels { get; set; }
        public List<int> EventIds { get; set; }
        public List<string> Sources { get; set; }
        public string ProjectKey { get; set; }
        public string Priority { get; set; }
        public string Responders { get; set; }
        public string Tags { get; set; }

        public string InitialTimeEstimate { get; set; }
        public string RemainingTimeEstimate { get; set; }
        public string DueDate { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LogName))
                throw new InvalidOperationException($"A {nameof(LogName)} must be specified for the listener.");
        }

        public void Start(bool isInteractive = false)
        {
            try
            {
                Log.Information().AddProperty("LogName", LogName).AddProperty("LogLevels", LogLevels)
                    .AddProperty("EventIds", EventIds).AddProperty("Sources", Sources)
                    .AddProperty("ProjectKey", ProjectKey, false, false).AddProperty("Priority", Priority, false, false)
                    .AddProperty("Responders", Responders, false, false).AddProperty("Tags", Extensions.GetArray(Tags), false, false)
                    .AddProperty("InitialTimeEstimate", InitialTimeEstimate, false, false)
                    .AddProperty("RemainingTimeEstimate", RemainingTimeEstimate, false, false).AddProperty("DueDate", DueDate, false, false)
                    .Add("Starting listener for {LogName:l} on {MachineName:l}", LogName);

                //Update this to calculate a query
                _eventLog = new EventLogQuery(LogName, PathType.LogName, "*");

                _isInteractive = isInteractive;
                _watcher = new EventLogWatcher(_eventLog);
                _watcher.EventRecordWritten += OnEntryWritten;
                _watcher.Enabled = true;
                _started = true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message).Add(
                    "Failed to start listener for {LogName:l} on {MachineName:l}: {Message:l}",
                    LogName);
            }
        }

        public void Stop()
        {
            try
            {
                if (!_started)
                    return;

                _cancel.Cancel();
                _watcher.Enabled = false;
                _watcher.Dispose();

                Log.Debug().Add("Listener stopped for {LogName:l} on {MachineName:l}", LogName, MachineName ?? ".");
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Failed to stop listener: {Message:l}", ex.Message);
            }
        }

        private async void OnEntryWritten(object sender, EventRecordWrittenEventArgs args)
        {
            try
            {
                //Ensure that events are new and have not been seen already. This addresses a scenario where event logs can repeatedly pass events to the handler.
                if (args.EventRecord != null && args.EventRecord.TimeCreated >= ServiceManager.ServiceStart)
                    await Task.Run(() => HandleEventLogEntry(args.EventRecord));
                else if (args.EventRecord != null && args.EventRecord.TimeCreated < ServiceManager.ServiceStart)
                    ServiceManager.OldEvents++;
                else if (args.EventRecord == null)
                    ServiceManager.EmptyEvents++;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Failed to handle {EventLog} log entry: {Message:l}", LogName, ex.Message);
            }
        }

        private void HandleEventLogEntry(EventRecord entry)
        {
            // Don't send the entry to Seq if it doesn't match the filtered log levels, event IDs, or sources
            if (LogLevels != null && LogLevels.Count > 0 && entry.Level != null &&
                !LogLevels.Contains((byte)entry.Level))
            {
                ServiceManager.UnhandledEvents++;
                return;
            }

            // EventID is obsolete
            if (EventIds != null && EventIds.Count > 0 && !EventIds.Contains(entry.Id))
            {
                ServiceManager.UnhandledEvents++;
                return;
            }

            if (Sources != null && Sources.Count > 0 && !Sources.Contains(entry.ProviderName))
            {
                ServiceManager.UnhandledEvents++;
                return;
            }

            try
            {
                Log.Level(Extensions.MapLogLevel(entry)).AddProperty("LogName", LogName)
                    .SetTimestamp(entry.TimeCreated ?? DateTime.Now)
                    .AddProperty("Provider", entry.ProviderName).AddProperty("EventId", entry.Id)
                    .AddProperty("KeywordNames", entry.KeywordsDisplayNames)
                    .AddProperty("EventLevel", entry.LevelDisplayName)
                    .AddProperty("EventLevelId", entry.Level).AddProperty(ParseXml(entry.ToXml()))
                    .AddProperty("Description", entry.FormatDescription())
                    .AddProperty("Summary", GetMessage(entry.FormatDescription()))
                    .AddProperty("ProjectKey", ProjectKey, false, false).AddProperty("Priority", Priority, false, false)
                    .AddProperty("Responders", Responders, false, false).AddProperty("Tags", Extensions.GetArray(Tags), false, false)
                    .AddProperty("InitialTimeEstimate", InitialTimeEstimate, false, false)
                    .AddProperty("RemainingTimeEstimate", RemainingTimeEstimate, false, false).AddProperty("DueDate", DueDate, false, false)
                    .Add("[{LogName:l}] - ({EventLevel:l}) - Event Id {EventId} - {Summary:l}");

                ServiceManager.EventsProcessed++;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Error parsing event: {Message:l}", ex.Message);
            }
        }

        private Dictionary<string, object> ParseXml(string xml)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(xml))
                return result;

            try
            {
                var xmlDoc = XElement.Parse(xml);
                return ProcessNode(xmlDoc);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(ex.Message);
                return new Dictionary<string, object>();
            }
        }

        private Dictionary<string, object> ProcessNode(XElement element, int depth = 0, string name = null)
        {
            var result = new Dictionary<string, object>();
            var nodeName = !string.IsNullOrEmpty(name) ? name : element.Name.LocalName;

            if (!element.HasElements && !element.IsEmpty)
                result.Add(nodeName, element.Value);
            else
                foreach (var descendant in element.Elements())
                foreach (var node in ProcessNode(descendant, depth + 1,
                    depth > 0 && !nodeName.Equals("System", StringComparison.OrdinalIgnoreCase)
                        ? string.Format($"{nodeName}-{GetName(descendant)}")
                        : GetName(descendant)))
                    result.Add(node.Key, node.Value);

            return result;
        }

        private string GetName(XElement element)
        {
            if (element.HasAttributes &&
                element.FirstAttribute.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                return element.FirstAttribute.Value;
            return element.Name.LocalName;
        }

        private string GetMessage(string message)
        {
            return message.Contains(Environment.NewLine) &&
                   !string.IsNullOrEmpty(message.Substring(0,
                       message.IndexOf(Environment.NewLine, StringComparison.Ordinal)))
                ? message.Substring(0, message.IndexOf(Environment.NewLine, StringComparison.Ordinal))
                : message;
        }
    }
}