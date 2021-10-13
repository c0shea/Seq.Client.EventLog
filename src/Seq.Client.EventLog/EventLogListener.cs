using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using System.Threading.Tasks;
using Lurgle.Logging;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Seq.Client.EventLog
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class EventLogListener
    {
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private EventLogQuery _eventLog;

        // ReSharper disable once NotAccessedField.Local
        private bool _isInteractive;
        private volatile bool _started;
        private EventLogWatcher _watcher;

        public string LogName { get; set; }

        //Logged as RemoteServer to avoid conflict with the inbuilt MachineName property
        public string MachineName { get; set; }

        public string MessageTemplate { get; set; } =
            "[{LogName:l}] - ({EventLevel:l}) - Event Id {EventId} - {Summary:l}";

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

        public bool ProcessRetroactiveEntries { get; set; }

        //When ProcessRetroactiveEntries isn't desirable, but processing events that occurred while the service was stopped is needed, use StoreLastEntry. ProcessRetroactiveEntries = true always supersedes this.
        public bool StoreLastEntry { get; set; }

        //This is used to save the current place in the logs on service exit
        public EventBookmark CurrentBookmark { get; set; }

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
                    .AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("ProjectKey", ProjectKey, false, false)
                    .AddProperty("Priority", Priority, false, false)
                    .AddProperty("Responders", Responders, false, false)
                    .AddProperty("Tags", Extensions.GetArray(Tags), false, false)
                    .AddProperty("InitialTimeEstimate", InitialTimeEstimate, false, false)
                    .AddProperty("RemainingTimeEstimate", RemainingTimeEstimate, false, false)
                    .AddProperty("DueDate", DueDate, false, false)
                    .Add("Starting {ListenerType:l} listener for {LogName:l} on {MachineName:l}",
                        Extensions.GetListenerType(MachineName), LogName);

                _eventLog = new EventLogQuery(LogName, PathType.LogName, "*");
                _isInteractive = isInteractive;
                var session = new EventLogSession();
                if (string.IsNullOrEmpty(MachineName))
                    session = new EventLogSession(MachineName);
                _eventLog.Session = session;
                _watcher = GetWatcherConfig();
                _watcher.EventRecordWritten += OnEntryWritten;
                _watcher.Enabled = true;
                _started = true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .AddProperty("RemoteServer", MachineName, false, false).Add(
                        "Failed to start {ListenerType:l} listener for {LogName:l} on {MachineName:l}: {Message:l}",
                        Extensions.GetListenerType(MachineName), LogName);
            }
        }

        private EventLogWatcher GetWatcherConfig()
        {
            var eventLog = new EventLogReader(_eventLog);

            if (CurrentBookmark != null && (ProcessRetroactiveEntries || StoreLastEntry))
            {
                //Go back a position to allow the bookmark to be read
                ServiceManager.SaveOnExit = true;
                eventLog.Seek(CurrentBookmark, -1);
                var checkBookmark = eventLog.ReadEvent();

                if (checkBookmark != null)
                {
                    checkBookmark.Dispose();
                    Log.Debug().Add("Logging from last bookmark for {LogName:l} on {MachineName:l}", LogName);
                    return new EventLogWatcher(_eventLog, CurrentBookmark, true);
                }

                Log.Debug().Add(
                    "Cannot find last bookmark for {LogName:l} on {MachineName:l} - processing new events",
                    LogName);
            }
            else if (ProcessRetroactiveEntries)
            {
                ServiceManager.SaveOnExit = true;
                var firstEvent = eventLog.ReadEvent();
                if (firstEvent != null)
                {
                    Log.Debug().Add("Logging from first logged event for {LogName:l} on {MachineName:l}", LogName);
                    return new EventLogWatcher(_eventLog, firstEvent.Bookmark, true);
                }

                Log.Debug().Add(
                    "Cannot determine first event for {LogName:l} on {MachineName:l} - processing new events",
                    LogName);
            }
            else
            {
                Log.Debug().Add("Processing new events for {LogName:l} on {MachineName:l}", LogName);
            }

            return new EventLogWatcher(_eventLog);
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

                Log.Debug().AddProperty("RemoteServer", MachineName, false, false).Add(
                    "{ListenerType:l} listener stopped for {LogName:l} on {MachineName:l}",
                    Extensions.GetListenerType(MachineName), LogName);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .AddProperty("RemoteServer", MachineName, false, false).Add(
                        "Failed to stop {ListenerType:l} listener for {LogName:l} on {MachineName:l}: {Message:l}",
                        Extensions.GetListenerType(MachineName), LogName);
            }
        }

        private async void OnEntryWritten(object sender, EventRecordWrittenEventArgs args)
        {
            try
            {
                //Ensure that events are new and have not been seen already. This addresses a scenario where event logs can repeatedly pass events to the handler.
                if (args.EventRecord != null && (ProcessRetroactiveEntries || StoreLastEntry || !ProcessRetroactiveEntries &&
                    args.EventRecord.TimeCreated >= ServiceManager.ServiceStart))
                    await Task.Run(() => HandleEventLogEntry(args.EventRecord));
                else if (args.EventRecord != null && !ProcessRetroactiveEntries &&
                         args.EventRecord.TimeCreated < ServiceManager.ServiceStart)
                    ServiceManager.OldEvents++;
                else if (args.EventRecord == null)
                    ServiceManager.EmptyEvents++;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("Message", ex.Message)
                    .Add("Failed to handle {ListenerType:l} {LogName:l} log entry on {MachineName:l}: {Message:l}",
                        Extensions.GetListenerType(MachineName), LogName);
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
                if (ProcessRetroactiveEntries || StoreLastEntry)
                    CurrentBookmark = entry.Bookmark;

                Log.Level(Extensions.MapLogLevel(entry))
                    .SetTimestamp(entry.TimeCreated ?? DateTime.Now)
                    .AddProperty("LogName", LogName)
                    .AddProperty("LogLevels", LogLevels)
                    .AddProperty("EventIds", EventIds)
                    .AddProperty("Sources", Sources)
                    .AddProperty("Provider", entry.ProviderName)
                    .AddProperty("EventId", entry.Id)
                    .AddProperty("KeywordNames", entry.KeywordsDisplayNames)
                    .AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .AddProperty("EventLevel", entry.LevelDisplayName)
                    .AddProperty("EventLevelId", entry.Level)
                    .AddProperty("Description", entry.FormatDescription())
                    .AddProperty("Summary", Extensions.GetMessage(entry.FormatDescription()))
                    .AddProperty("ProjectKey", ProjectKey, false, false)
                    .AddProperty("Priority", Priority, false, false)
                    .AddProperty("Responders", Responders, false, false)
                    .AddProperty("Tags", Extensions.GetArray(Tags), false, false)
                    .AddProperty("InitialTimeEstimate", InitialTimeEstimate, false, false)
                    .AddProperty("RemainingTimeEstimate", RemainingTimeEstimate, false, false)
                    .AddProperty("DueDate", DueDate, false, false)
                    .AddProperty(Extensions.ParseXml(entry.ToXml()))
                    .Add(MessageTemplate);

                ServiceManager.EventsProcessed++;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .AddProperty("RemoteServer", MachineName, false, false).Add(
                        "Error parsing {ListenerType:l} {LogName:l} event on {MachineName:l}: {Message:l}",
                        Extensions.GetListenerType(MachineName), LogName);
            }
        }
    }
}