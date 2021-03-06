﻿using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Serilog;

namespace Seq.Client.EventLog
{
    class EventLogClient
    {
        private List<EventLogListener> _eventLogListeners;

        public void Start(string configuration = null)
        {
            LoadListeners(configuration);
            ValidateListeners();
            StartListeners();
        }

        public void Stop()
        {
            StopListeners();
        }

        private void LoadListeners(string configuration)
        {
            string filePath;
            if (configuration == null)
            {
                var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                filePath = Path.Combine(directory ?? ".", "EventLogListeners.json");
            }
            else
            {
                filePath = configuration;
            }

            Log.Information("Loading listener configuration from {ConfigurationFilePath}", filePath);
            var file = File.ReadAllText(filePath);

            _eventLogListeners = JsonConvert.DeserializeObject<List<EventLogListener>>(file);
        }

        private void ValidateListeners()
        {
            foreach (var listener in _eventLogListeners)
            {
                listener.Validate();
            }
        }

        private void StartListeners()
        {
            foreach (var listener in _eventLogListeners)
            {
                listener.Start();
            }
        }

        private void StopListeners()
        {
            foreach (var listener in _eventLogListeners)
            {
                listener.Stop();
            }
        }
    }
}
