using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Lurgle.Logging;
using Newtonsoft.Json;
using Timer = System.Timers.Timer;

// ReSharper disable MemberCanBePrivate.Global

namespace Seq.Client.EventLog
{
    public static class ServiceManager
    {
        private static Timer _heartbeatTimer;
        public static readonly DateTime ServiceStart = DateTime.Now;
        public static long EventsProcessed;
        public static long LastEventsProcessed;
        public static long LastProcessed;
        public static long UnhandledEvents;
        public static long OldEvents;
        public static long LogonsDetected;
        public static long NonInteractiveLogons;

        public static long EmptyEvents;

        //This will be set if any listener is watching for Windows logins
        public static bool WindowsLogins;

        //This will be set if any listener has ProcessRetroactiveEntries enabled
        public static bool SaveBookmarks;
        private static bool _isInteractive;
        private static DateTime _lastTime = DateTime.Now;
        public static string JsonConfigPath { get; set; }
        public static List<EventLogListener> EventLogListeners { get; set; }

        private static int ZeroLogHeartbeats { get; set; } = 0;

        public static void Start(bool isInteractive)
        {
            _isInteractive = isInteractive;
            //Heartbeat timer that can be used to detect if the service is not running
            if (Config.HeartbeatInterval <= 0)
            {
                if (Config.IsDebug)
                    Log.Debug().AddProperty("HeartbeatName", $"{Config.AppName} Heartbeat")
                        .Add("[{HeartbeatName:l} - {MachineName:l}] Heartbeat is disabled ...");
                return;
            }

            //First heartbeat will be at a random interval between 2 and 10 seconds
            _heartbeatTimer = isInteractive
                ? new Timer {Interval = 10000}
                : new Timer {Interval = new Random().Next(2000, 10000)};
            _heartbeatTimer.Elapsed += ServiceHeartbeat;
            _heartbeatTimer.AutoReset = false;
            _heartbeatTimer.Start();
        }

        public static void Stop()
        {
            _heartbeatTimer.Stop();
        }

        public static void LoadListeners(string configuration)
        {
            if (configuration == null)
            {
                var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                JsonConfigPath = Path.Combine(directory ?? ".", "EventLogListeners.json");
            }
            else
            {
                JsonConfigPath = configuration;
            }

            if (Config.IsDebug)
                Log.Debug()
                    .Add("Loading listener configuration from {ConfigurationFilePath:l} on {MachineName:l} ...",
                        JsonConfigPath);
            var file = File.ReadAllText(JsonConfigPath);

            EventLogListeners = JsonConvert.DeserializeObject<List<EventLogListener>>(file);
        }

        public static void ValidateListeners()
        {
            foreach (var listener in EventLogListeners) listener.Validate();
        }

        public static void SaveListeners()
        {
            try
            {
                var json = JsonConvert.SerializeObject(EventLogListeners, Formatting.Indented);

                if (Config.IsDebug)
                    Log.Debug()
                        .Add("Saving listener configuration to {ConfigurationFilePath:l} on {MachineName:l} ...",
                            JsonConfigPath);
                File.WriteAllText(JsonConfigPath, json);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .Add("Error saving {ConfigurationFilePath:l} on {MachineName:l}: {Message:l}", JsonConfigPath);
            }
        }

        public static void StartListeners(bool isInteractive = false)
        {
            foreach (var listener in EventLogListeners) listener.Start(isInteractive);
        }

        public static void StopListeners()
        {
            foreach (var listener in EventLogListeners) listener.Stop();
        }

        private static void ServiceHeartbeat(object sender, ElapsedEventArgs e)
        {
            var timeNow = DateTime.Now;
            var diff = EventsProcessed - LastEventsProcessed;

            if (timeNow.Day != _lastTime.Day)
            {
                Log.Debug().AddProperty("HeartbeatName", $"{Config.AppName} Heartbeat")
                    .Add("[{HeartbeatName:l} - {MachineName:l}] Day rollover, resetting counters ...");
                EventsProcessed = 0;
                LastEventsProcessed = 0;
                LastProcessed = 0;
                UnhandledEvents = 0;
                OldEvents = 0;
                EmptyEvents = 0;
                LogonsDetected = 0;
                NonInteractiveLogons = 0;
            }

            var serviceCounters = new Dictionary<string, object>
            {
                {"EventsProcessed", diff}, {"TotalProcessed", EventsProcessed}, {"OldEvents", OldEvents},
                {"EmptyEvents", EmptyEvents}, {"UnhandledEvents", UnhandledEvents}
            };

            if (WindowsLogins)
            {
                serviceCounters.Add("LogonsDetected", LogonsDetected);
                serviceCounters.Add("NonInteractiveLogons", NonInteractiveLogons);
            }

            if (Config.HeartbeatsBeforeReset > 0)
            {
                if (EventsProcessed + UnhandledEvents + OldEvents + EmptyEvents + LogonsDetected + NonInteractiveLogons > LastProcessed)
                    ZeroLogHeartbeats = 0;
                else
                    ZeroLogHeartbeats++;

                if (ZeroLogHeartbeats > Config.HeartbeatsBeforeReset)
                {
                    Log.Warning().AddProperty("HeartbeatName", $"{Config.AppName} Heartbeat")
                        .AddProperty("HeartbeatCount", ZeroLogHeartbeats)
                        .AddProperty("NextTime", timeNow.AddMilliseconds(_isInteractive ? 60000 : Config.HeartbeatInterval * 1000))
                        .Add("[{HeartbeatName:l} - {MachineName:l}] - Detected that no new log entries have been seen in the past {HeartbeatCount} heartbeats, resetting listeners ...");

                    StopListeners(); 
                    Thread.Sleep(1000);
                    StartListeners(_isInteractive);
                }
            }

            Log.Debug()
                .AddProperty("HeartbeatName", $"{Config.AppName} Heartbeat")
                .AddProperty(serviceCounters)
                .AddProperty("NextTime",
                    timeNow.AddMilliseconds(_isInteractive ? 60000 : Config.HeartbeatInterval * 1000))
                .Add(
                    Config.IsDebug
                        ? WindowsLogins
                            ? "[{HeartbeatName:l} - {MachineName:l}] - Events Processed: {EventsProcessed}, Total Processed: {TotalProcessed}, " +
                              "LogonsDetected: {LogonsDetected}, NonInteractiveLogons: {NonInteractiveLogons}, " +
                              "Unhandled events: {UnhandledEvents}, Old events seen: {OldEvents}, Empty events: {EmptyEvents}, Next Heartbeat: {NextTime:H:mm:ss tt}"
                            : "[{HeartbeatName:l} - {MachineName:l}] - Events Processed: {EventsProcessed}, Total Processed: {TotalProcessed}, " +
                              "Unhandled events: {UnhandledEvents}, Old events seen: {OldEvents}, Empty events: {EmptyEvents}, Next Heartbeat: {NextTime:H:mm:ss tt}"
                        : WindowsLogins
                            ? "[{HeartbeatName:l} - {MachineName:l}] - Events Processed: {EventsProcessed}, Total Processed: {TotalProcessed}, " +
                              "LogonsDetected: {LogonsDetected}, NonInteractiveLogons: {NonInteractiveLogons}, Next Heartbeat: {NextTime:H:mm:ss tt}"
                            : "[{HeartbeatName:l} - {MachineName:l}] - Events Processed: {EventsProcessed}, Total Processed: {TotalProcessed}, " +
                              "Next Heartbeat: {NextTime:H:mm:ss tt}");

            LastProcessed = EventsProcessed + UnhandledEvents + OldEvents + EmptyEvents + LogonsDetected + NonInteractiveLogons;
            LastEventsProcessed = EventsProcessed;
            _lastTime = timeNow;

            //Periodically save the listener config if we are storing this
            if (SaveBookmarks)
                SaveListeners();

            if (_heartbeatTimer.AutoReset)
                return;

            //Set the timer to 60 seconds (interactive) or configured heartbeat (service) after initial heartbeat
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Interval = _isInteractive ? 60000 : Config.HeartbeatInterval * 1000;
            _heartbeatTimer.Start();
        }
    }
}