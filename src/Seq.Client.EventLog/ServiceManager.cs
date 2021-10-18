using System;
using System.Collections.Generic;
using System.Timers;
using Lurgle.Logging;

// ReSharper disable MemberCanBePrivate.Global

namespace Seq.Client.EventLog
{
    public static class ServiceManager
    {
        private static Timer _heartbeatTimer;
        public static readonly DateTime ServiceStart = DateTime.Now;
        public static long EventsProcessed;
        public static long LastProcessed;
        public static long UnhandledEvents;
        public static long OldEvents;
        public static long LogonsDetected;
        public static long NonInteractiveLogons;

        public static long EmptyEvents;

        //This will be set if any listener is watching for Windows logins
        public static bool WindowsLogins;
        //This will be set if any listener has ProcessRetroactiveEntries enabled
        public static bool SaveOnExit;
        private static bool _isInteractive;
        private static DateTime _lastTime = DateTime.Now;


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
                ? new Timer { Interval = 10000 }
                : new Timer { Interval = new Random().Next(2000, 10000) };
            _heartbeatTimer.Elapsed += ServiceHeartbeat;
            _heartbeatTimer.AutoReset = false;
            _heartbeatTimer.Start();
        }

        public static void Stop()
        {
            _heartbeatTimer.Stop();
        }

        private static void ServiceHeartbeat(object sender, ElapsedEventArgs e)
        {
            var timeNow = DateTime.Now;
            var diff = EventsProcessed - LastProcessed;

            if (timeNow.Day != _lastTime.Day)
            {
                Log.Debug().AddProperty("HeartbeatName", $"{Config.AppName} Heartbeat")
                    .Add("[{HeartbeatName:l} - {MachineName:l}] Day rollover, resetting counters ...");
                EventsProcessed = 0;
                LastProcessed = 0;
                UnhandledEvents = 0;
                OldEvents = 0;
                EmptyEvents = 0;
                LogonsDetected = 0;
                NonInteractiveLogons = 0;
            }

            var serviceCounters = new Dictionary<string, object>
            {
                { "EventsProcessed", diff }, { "TotalProcessed", EventsProcessed }, { "OldEvents", OldEvents },
                { "EmptyEvents", EmptyEvents }, { "UnhandledEvents", UnhandledEvents }
            };

            if (WindowsLogins)
            {
                serviceCounters.Add("LogonsDetected", LogonsDetected);
                serviceCounters.Add("NonInteractiveLogons", NonInteractiveLogons);
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

            LastProcessed = EventsProcessed;
            _lastTime = timeNow;

            if (_heartbeatTimer.AutoReset) return;
            //Set the timer to 60 seconds (interactive) or configured heartbeat (service) after initial heartbeat
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Interval = _isInteractive ? 60000 : Config.HeartbeatInterval * 1000;
            _heartbeatTimer.Start();
        }
    }
}