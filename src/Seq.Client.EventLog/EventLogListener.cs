using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Seq.Client.EventLog
{
    public class EventLogListener
    {
        public string LogName { get; set; }
        public string MachineName { get; set; }
        public bool ProcessRetroactiveEntries { get; set; }

        // These properties allow for the filtering of events that will be sent to Seq.
        // If they are not specified in the JSON, all events in the log will be sent.
        public List<EventLogEntryType> LogLevels { get; set; }
        public List<int> EventIds { get; set; }
        public List<string> Sources { get; set; }

        private System.Diagnostics.EventLog _eventLog;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private Task _retroactiveLoadingTask;
        private volatile bool _started;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(LogName))
            {
                throw new InvalidOperationException($"A {nameof(LogName)} must be specified for the listener.");
            }
        }

        public void Start()
        {
            try
            {
                Serilog.Log.Information("Starting listener for {LogName} on {MachineName}", LogName, MachineName ?? ".");

                _eventLog = OpenEventLog();

                if (ProcessRetroactiveEntries)
                {
                    // Start as a new task so it doesn't block the startup of the service. This has
                    // to go on its own thread to avoid deadlocking via `Wait()`/`Result`.
                    _retroactiveLoadingTask = Task.Factory.StartNew(SendRetroactiveEntries, TaskCreationOptions.LongRunning);
                }

                _eventLog.EntryWritten += OnEntryWritten;
                _eventLog.EnableRaisingEvents = true;
                _started = true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to start listener for {LogName} on {MachineName}", LogName, MachineName ?? ".");
            }
        }

        System.Diagnostics.EventLog OpenEventLog()
        {
            var eventLog = new System.Diagnostics.EventLog(LogName);
            if (!string.IsNullOrWhiteSpace(MachineName))
            {
                eventLog.MachineName = MachineName;
            }

            return eventLog;
        }

        public void Stop()
        {
            try
            {
                if (!_started)
                    return;

                _cancel.Cancel();
                _eventLog.EnableRaisingEvents = false;

                // This would be a little racy if start and stop were ever called on different threads, but
                // this isn't done, currently.
                _retroactiveLoadingTask?.Wait();

                _eventLog.Close();
                _eventLog.Dispose();

                Serilog.Log.Information("Listener stopped");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to stop listener");
            }
        }

        private void SendRetroactiveEntries()
        {
            try
            {
                using (var eventLog = OpenEventLog())
                {
                    Serilog.Log.Information("Processing {EntryCount} retroactive entries in {LogName}", eventLog.Entries.Count, LogName);

                    foreach (EventLogEntry entry in eventLog.Entries)
                    {
                        if (_cancel.IsCancellationRequested)
                        {
                            Serilog.Log.Warning("Canceling retroactive event loading");
                            return;
                        }

                        HandleEventLogEntry(entry, eventLog.Log).GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to send retroactive entries in {LogName} on {MachineName}", LogName, MachineName ?? ".");
            }
        }

        private void OnEntryWritten(object sender, EntryWrittenEventArgs args)
        {
            try
            {
                HandleEventLogEntry(args.Entry, _eventLog.Log).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to handle an event log entry");
            }
        }

        private async Task HandleEventLogEntry(EventLogEntry entry, string logName)
        {
            // Don't send the entry to Seq if it doesn't match the filtered log levels, event IDs, or sources
            if (LogLevels != null && LogLevels.Count > 0 && !LogLevels.Contains(entry.EntryType))
                return;

            // EventID is obsolete
#pragma warning disable 618
            if (EventIds != null && EventIds.Count > 0 && !EventIds.Contains(entry.EventID))
#pragma warning restore 618
                return;

            if (Sources != null && Sources.Count > 0 && !Sources.Contains(entry.Source))
                return;

            await SeqApi.PostRawEvents(entry.ToDto(logName));
        }
    }
}
