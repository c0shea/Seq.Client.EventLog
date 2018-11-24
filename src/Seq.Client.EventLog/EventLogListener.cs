using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Seq.Client.EventLog.Properties;

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

                _eventLog = new System.Diagnostics.EventLog(LogName);

                if (!string.IsNullOrWhiteSpace(MachineName))
                {
                    _eventLog.MachineName = MachineName;
                }
                
                _eventLog.EntryWritten += (sender, args) =>
                {
                    HandleEventLogEntry(args.Entry, _eventLog.Log);
                };
                _eventLog.EnableRaisingEvents = true;

                if (ProcessRetroactiveEntries)
                {
                    // Start as a new task so it doesn't block the startup of the service. This has
                    // to go on its own thread to avoid deadlocking via `Wait()`/`Result`.
                    _retroactiveLoadingTask = Task.Factory.StartNew(HandleRetroactiveEntries, TaskCreationOptions.LongRunning);
                }

                _started = true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to start listener for {LogName} on {MachineName}", LogName, MachineName ?? ".");
            }
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
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to stop listener");
            }
        }

        private void HandleRetroactiveEntries()
        {
            try
            {
                Serilog.Log.Information("Processing {EntryCount} retroactive entries in {LogName}", _eventLog.Entries.Count, LogName);

                foreach (EventLogEntry entry in _eventLog.Entries)
                {
                    if (_cancel.IsCancellationRequested)
                    {
                        Serilog.Log.Warning("Canceling retroactive event loading");
                        return;
                    }

                    HandleEventLogEntry(entry, _eventLog.Log);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to handle retroactive entries");
            }
        }

        private void HandleEventLogEntry(EventLogEntry entry, string logName)
        {
            try
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

                PostRawEvents(entry.ToDto(logName));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to handle an event log entry");
            }
        }

        private static void PostRawEvents(RawEvents rawEvents)
        {
            using (var client = new HttpClient())
            {
                var uri = Settings.Default.SeqUri + "/api/events/raw";

                if (!string.IsNullOrWhiteSpace(Settings.Default.ApiKey))
                {
                    uri += "?apiKey=" + Settings.Default.ApiKey;
                }

                var content = new StringContent(
                    JsonConvert.SerializeObject(rawEvents, Formatting.None),
                    Encoding.UTF8,
                    "application/json");

                var result = client.PostAsync(uri, content).Result;
                if (!result.IsSuccessStatusCode)
                {
                    Serilog.Log.Error("Received failure status code {StatusCode} from Seq: {ReasonPhrase}", result.StatusCode, result.ReasonPhrase);
                }
            }
        }
    }
}
