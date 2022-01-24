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

        //Allow a per-log appname to be specified
        public string LogAppName { get; set; }
        public string LogName { get; set; }

        //Logged as RemoteServer to avoid conflict with the inbuilt MachineName property
        public string MachineName { get; set; }

        public string MessageTemplate { get; set; }


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

        //Optional mode that monitors successful interactive  logins
        public bool WindowsLogins { get; set; }

        //Default to filtering empty guids for Windows logins
        public bool GuidIsEmpty { get; set; }
        public bool ProcessRetroactiveEntries { get; set; }

        //When ProcessRetroactiveEntries isn't desirable, but processing events that occurred while the service was stopped is needed, use StoreLastEntry. ProcessRetroactiveEntries = true always supersedes this.
        public bool StoreLastEntry { get; set; }

        //This is used to save the current place in the logs on service exit
        public EventBookmark CurrentBookmark { get; set; }

        public void Validate()
        {
            if (string.IsNullOrEmpty(LogAppName))
                LogAppName = Config.AppName;

            if (string.IsNullOrEmpty(MessageTemplate))
                MessageTemplate = WindowsLogins
                    ? "[{LogAppName:l}] - New login detected on {MachineName:l} - {EventData_TargetDomainName:l}\\{EventData_TargetUserName:l} at {EventTime:F}"
                    : "[{LogAppName:l}] - ({EventLevel:l}) - Event Id {EventId} - {EventSummary:l}";

            if (WindowsLogins)
            {
                LogName = "Security";
                LogLevels = new List<byte>();
                EventIds = new List<int> {4624};
                Sources = new List<string>();
                ServiceManager.WindowsLogins = true;
            }

            if (string.IsNullOrWhiteSpace(LogName))
                throw new InvalidOperationException($"A {nameof(LogName)} must be specified for the listener.");
        }

        public void Start(bool isInteractive = false)
        {
            try
            {
                Log.Information().AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .AddProperty("LogLevels", LogLevels)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .AddProperty("EventIds", EventIds)
                    .AddProperty("Sources", Sources)
                    .AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("WindowsLogins", WindowsLogins)
                    .AddProperty("GuidIsEmpty", GuidIsEmpty)
                    .AddProperty("ProcessRetroactiveEntries", ProcessRetroactiveEntries)
                    .AddProperty("StoreLastEntry", StoreLastEntry)
                    .AddProperty("ProjectKey", ProjectKey, false, false)
                    .AddProperty("Priority", Priority, false, false)
                    .AddProperty("Responders", Responders, false, false)
                    .AddProperty("Tags", Extensions.GetArray(Tags), false, false)
                    .AddProperty("InitialTimeEstimate", InitialTimeEstimate, false, false)
                    .AddProperty("RemainingTimeEstimate", RemainingTimeEstimate, false, false)
                    .AddProperty("DueDate", DueDate, false, false)
                    .Add(WindowsLogins
                        ? "[{LogAppName:l}] Starting Windows Logins listener for {LogName:l} on {MachineName:l}"
                        : "[{LogAppName:l}] Starting {ListenerType:l} listener ({LogAppName:l}) for {LogName:l} on {MachineName:l}");

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
                    .AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .AddProperty("RemoteServer", MachineName, false, false).Add(
                        "[{LogAppName:l}] Failed to start {ListenerType:l} listener for {LogName:l} on {MachineName:l}: {Message:l}");
            }
        }

        private EventLogWatcher GetWatcherConfig()
        {
            var eventLog = new EventLogReader(_eventLog);

            if (ProcessRetroactiveEntries || StoreLastEntry)
                ServiceManager.SaveBookmarks = true;

            if (CurrentBookmark != null && (ProcessRetroactiveEntries || StoreLastEntry))
            {
                //Go back a position to allow the bookmark to be read
                eventLog.Seek(CurrentBookmark, -1);
                var checkBookmark = eventLog.ReadEvent();

                if (checkBookmark != null)
                {
                    checkBookmark.Dispose();
                    Log.Debug().AddProperty("LogAppName", LogAppName)
                        .AddProperty("LogName", LogName)
                        .Add("[{LogAppName:l}] Logging from last bookmark for {LogName:l} on {MachineName:l}");
                    return new EventLogWatcher(_eventLog, CurrentBookmark, true);
                }

                Log.Debug().AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .Add(
                        "[{LogAppName:l}] Cannot find last bookmark for {LogName:l} on {MachineName:l} - processing new events");
            }
            else if (ProcessRetroactiveEntries)
            {
                var firstEvent = eventLog.ReadEvent();
                if (firstEvent != null)
                {
                    Log.Debug().AddProperty("LogAppName", LogAppName)
                        .AddProperty("LogName", LogName)
                        .Add("[{LogAppName:l}] Logging from first logged event for {LogName:l} on {MachineName:l}");
                    return new EventLogWatcher(_eventLog, firstEvent.Bookmark, true);
                }

                Log.Debug().AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .Add(
                        "[{LogAppName:l}] Cannot determine first event for {LogName:l} on {MachineName:l} - processing new events");
            }
            else
            {
                Log.Debug().AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .Add("[{LogAppName:l}] Processing new events for {LogName:l} on {MachineName:l}");
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

                Log.Debug().AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .Add("[{LogAppName:l}] {ListenerType:l} listener stopped for {LogName:l} on {MachineName:l}");
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .Add(
                        "[{LogAppName:l}] Failed to stop {ListenerType:l} listener for {LogName:l} on {MachineName:l}: {Message:l}");
            }
        }

        private async void OnEntryWritten(object sender, EventRecordWrittenEventArgs args)
        {
            try
            {
                //Ensure that events are new and have not been seen already. This addresses a scenario where event logs can repeatedly pass events to the handler.
                if (args.EventRecord != null && (ProcessRetroactiveEntries || StoreLastEntry ||
                                                 !ProcessRetroactiveEntries &&
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
                    .AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .Add(
                        "[{LogAppName:l}] Failed to handle {ListenerType:l} {LogName:l} log entry on {MachineName:l}: {Message:l}");
            }
        }

        private void HandleEventLogEntry(EventRecord entry)
        {
            // Don't send the entry to Seq if it doesn't match the filtered log levels, event IDs, or sources
            if (LogLevels != null && LogLevels.Count > 0 && entry.Level != null &&
                !LogLevels.Contains((byte) entry.Level))
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

                var eventProperties = Extensions.ParseXml(entry.ToXml());

                //Windows Logins handler
                if (WindowsLogins && eventProperties.TryGetValue("EventData_LogonType", out var logonType) &&
                    eventProperties.TryGetValue("EventData_IpAddress", out var ipAddress) &&
                    eventProperties.TryGetValue("EventData_LogonGuid", out var logonGuid))
                    switch (Config.GetInt(logonType))
                    {
                        case 2 when entry.Keywords != null &&
                                    ((StandardEventKeywords) entry.Keywords).HasFlag(StandardEventKeywords
                                        .AuditSuccess) && !Equals(ipAddress, "-") &&
                                    (GuidIsEmpty && logonGuid.Equals("{00000000-0000-0000-0000-000000000000}") ||
                                     !GuidIsEmpty && !logonGuid.Equals("{00000000-0000-0000-0000-000000000000}")):
                        case 10 when entry.Keywords != null &&
                                     ((StandardEventKeywords) entry.Keywords).HasFlag(StandardEventKeywords
                                         .AuditSuccess) && !Equals(ipAddress, "-") &&
                                     (GuidIsEmpty && logonGuid.Equals("{00000000-0000-0000-0000-000000000000}") ||
                                      !GuidIsEmpty && !logonGuid.Equals("{00000000-0000-0000-0000-000000000000}")):
                            ServiceManager.LogonsDetected++;
                            break;
                        default:
                            ServiceManager.NonInteractiveLogons++;
                            return;
                    }

                //Friendly event times
                var eventTimeLong = string.Empty;
                var eventTimeShort = string.Empty;
                if (entry.TimeCreated != null)
                {
                    eventTimeLong = ((DateTime) entry.TimeCreated).ToString("F");
                    eventTimeShort = ((DateTime) entry.TimeCreated).ToString("G");
                }

                Log.Level(Extensions.MapLogLevel(entry))
                    .SetTimestamp(entry.TimeCreated ?? DateTime.Now)
                    .AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .AddProperty("LogLevels", LogLevels)
                    .AddProperty("EventIds", EventIds)
                    .AddProperty("Sources", Sources)
                    .AddProperty("Provider", entry.ProviderName)
                    .AddProperty("EventId", entry.Id)
                    .AddProperty("EventTime", entry.TimeCreated)
                    .AddProperty("EventTimeLong", eventTimeLong)
                    .AddProperty("EventTimeShort", eventTimeShort)
                    .AddProperty("KeywordNames", entry.KeywordsDisplayNames)
                    .AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .AddProperty("EventLevel", entry.LevelDisplayName)
                    .AddProperty("EventLevelId", entry.Level)
                    .AddProperty("EventDescription", entry.FormatDescription())
                    .AddProperty("EventSummary", Extensions.GetMessage(entry.FormatDescription()))
                    .AddProperty("ProjectKey", ProjectKey, false, false)
                    .AddProperty("Priority", Priority, false, false)
                    .AddProperty("Responders", Responders, false, false)
                    .AddProperty("Tags", Extensions.GetArray(Tags), false, false)
                    .AddProperty("InitialTimeEstimate", InitialTimeEstimate, false, false)
                    .AddProperty("RemainingTimeEstimate", RemainingTimeEstimate, false, false)
                    .AddProperty("DueDate", DueDate, false, false)
                    .AddProperty(eventProperties)
                    .Add(MessageTemplate);

                ServiceManager.EventsProcessed++;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .AddProperty("RemoteServer", MachineName, false, false)
                    .AddProperty("LogAppName", LogAppName)
                    .AddProperty("LogName", LogName)
                    .AddProperty("ListenerType", Extensions.GetListenerType(MachineName))
                    .Add(
                        "[{LogAppName:l}] Error parsing {ListenerType:l} {LogName:l} event on {MachineName:l}: {Message:l}");
            }
        }
    }
}