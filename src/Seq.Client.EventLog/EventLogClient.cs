﻿namespace Seq.Client.EventLog
{
    internal class EventLogClient
    {
        public static void Start(bool isInteractive = false, string configuration = null)
        {
            ServiceManager.LoadListeners(configuration);
            ServiceManager.ValidateListeners();
            ServiceManager.StartListeners(isInteractive);
        }

        public static void Stop()
        {
            ServiceManager.StopListeners();
            if (ServiceManager.SaveBookmarks)
                ServiceManager.SaveListeners();
        }
    }
}