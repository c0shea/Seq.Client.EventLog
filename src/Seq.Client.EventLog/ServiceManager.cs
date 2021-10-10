using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Lurgle.Logging;

namespace Seq.Client.EventLog
{
    public static class ServiceManager
    {
        private static Timer _heartbeatTimer;
        public static readonly DateTime ServiceStart = DateTime.Now;
        public static long EventsProcessed;
        public static long UnhandledEvents;
        public static long OldEvents;
        public static long EmptyEvents;
        public static TimedEventBag EventList { get; private set; }
        private static bool _isInteractive;

        static ServiceManager()
        {
            EventList = new TimedEventBag(600);
        }

        public static void Start(bool isInteractive)
        {
            _isInteractive = isInteractive;
            //Heartbeat timer that can be used to detect if the service is not running
            if (Config.HeartbeatInterval <= 0) return;
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

        private static void ServiceHeartbeat(object sender, ElapsedEventArgs e)
        {
            Log.Debug()
                .AddProperty("ItemCount", EventList.Count)
                .AddProperty("EventsProcessed", EventsProcessed)
                .AddProperty("OldEvents", OldEvents)
                .AddProperty("EmptyEvents", EmptyEvents)
                .AddProperty("UnhandledEvents", UnhandledEvents)
                .AddProperty("NextTime", DateTime.Now.AddMilliseconds(Config.HeartbeatInterval))
                .Add(
                    Config.IsDebug
                        ? "{AppName:l} Heartbeat [{MachineName:l}] - Event cache: {ItemCount}, Logons detected: {LogonsDetected}, " +
                          "Non-interactive logons: {NonInteractiveLogons}, Unhandled events: {UnhandledEvents}, Old events seen: {OldEvents}, " +
                          "Empty events: {EmptyEvents}, Next Heartbeat: {NextTime:H:mm:ss tt}"
                        : "{AppName:l} Heartbeat [{MachineName:l}] - Event cache: {ItemCount}, Next Heartbeat: {NextTime:H:mm:ss tt}");

            if (_heartbeatTimer.AutoReset) return;
            //Set the timer to 10 minutes after initial heartbeat
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Interval = _isInteractive ? 10000 : Config.HeartbeatInterval;
            _heartbeatTimer.Start();
        }
    }
}
