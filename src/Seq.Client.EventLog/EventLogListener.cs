using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
using System.Threading.Tasks;
using Lurgle.Logging;
using Timer = System.Timers.Timer;

namespace Seq.Client.EventLog
{
    public class EventLogListener
    {
        private bool _isInteractive;

        public string LogName { get; set; }
        public string MachineName { get; set; }
        public bool ProcessRetroactiveEntries { get; set; }

        // These properties allow for the filtering of events that will be sent to Seq.
        // If they are not specified in the JSON, all events in the log will be sent.
        public List<byte> LogLevels { get; set; }
        public List<int> EventIds { get; set; }
        public List<string> Sources { get; set; }


        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private EventLogQuery _eventLog;
        private volatile bool _started;
        private EventLogWatcher _watcher;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LogName))
            {
                throw new InvalidOperationException($"A {nameof(LogName)} must be specified for the listener.");
            }
        }

        public void Start(bool isInteractive = false)
        {
            try
            {
                Log.Information().Add("Starting listener for {LogName:l} on {MachineName:l}", LogName,
                    MachineName ?? ".");

                //Update this to calculate a query
                _eventLog = new EventLogQuery("Security", PathType.LogName,
                    "*[System[band(Keywords,9007199254740992) and (EventID=4624)]]");

                //if (ProcessRetroactiveEntries)
                //{
                //    // Start as a new task so it doesn't block the startup of the service. This has
                //    // to go on its own thread to avoid deadlocking via `Wait()`/`Result`.
                //    _retroactiveLoadingTask = Task.Factory.StartNew(SendRetroactiveEntries, TaskCreationOptions.LongRunning);
                //}

                _isInteractive = isInteractive;
                _watcher = new EventLogWatcher(_eventLog);
                _watcher.EventRecordWritten += OnEntryWritten;
                _watcher.Enabled = true;
                _started = true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Failed to start listener for {LogName:l} on {MachineName:l}: {Message:l}",
                    LogName, MachineName ?? ".", ex.Message);
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

                // This would be a little racy if start and stop were ever called on different threads, but
                // this isn't done, currently.
                //_retroactiveLoadingTask?.Wait();

                Log.Debug().Add("Listener stopped for {LogName:l} on {MachineName:l}", LogName, MachineName ?? ".");

            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Failed to stop listener: {Message:l}", ex.Message);
            }
        }

        //private void SendRetroactiveEntries()
        //{
        //    try
        //    {
        //        using (var eventLog = OpenEventLog())
        //        {
        //            Serilog.Log.Information("Processing {EntryCount} retroactive entries in {LogName}", eventLog.Entries.Count, LogName);

        //            foreach (EventLogEntry entry in eventLog.Entries)
        //            {
        //                if (_cancel.IsCancellationRequested)
        //                {
        //                    Serilog.Log.Warning("Canceling retroactive event loading");
        //                    return;
        //                }

        //                HandleEventLogEntry(entry, eventLog.Log).GetAwaiter().GetResult();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Serilog.Log.Error(ex, "Failed to send retroactive entries in {LogName} on {MachineName}", LogName, MachineName ?? ".");
        //    }
        //}

        private async void OnEntryWritten(object sender, EventRecordWrittenEventArgs args)
        {
            try
            {
                //Ensure that events are new and have not been seen already. This addresses a scenario where event logs can repeatedly pass events to the handler.
                if (args.EventRecord != null && args.EventRecord.TimeCreated >= ServiceManager.ServiceStart &&
                    !ServiceManager.EventList.Contains(args.EventRecord.RecordId))
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
            ServiceManager.EventList.Add(entry.RecordId);

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

                Log.Level(Extensions.MapLogLevel(entry.Level)).AddProperty("Xml", entry.ToXml())
                    .Add(entry.FormatDescription());
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Error parsing event: {Message:l}", ex.Message);
            }
        }
    }
}
