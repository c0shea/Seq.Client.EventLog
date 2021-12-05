using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Lurgle.Logging;
using Newtonsoft.Json;

namespace Seq.Client.EventLog
{
    internal class EventLogClient
    {
        private List<EventLogListener> _eventLogListeners;

        public void Start(bool isInteractive = false, string configuration = null)
        {
            LoadListeners(configuration);
            ValidateListeners();
            StartListeners(isInteractive);
        }

        public void Stop(string configuration = null)
        {
            StopListeners();
            if (ServiceManager.SaveOnExit)
                SaveListeners(configuration);
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

            Log.Information()
                .Add("Loading listener configuration from {ConfigurationFilePath:l} on {MachineName:l} ...", filePath);
            var file = File.ReadAllText(filePath);

            _eventLogListeners = JsonConvert.DeserializeObject<List<EventLogListener>>(file);
        }

        private void ValidateListeners()
        {
            foreach (var listener in _eventLogListeners) listener.Validate();
        }

        private void SaveListeners(string configuration)
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

            try
            {
                var json = JsonConvert.SerializeObject(_eventLogListeners, Formatting.Indented);

                Log.Information()
                    .Add("Saving listener configuration to {ConfigurationFilePath:l} on {MachineName:l} ...", filePath);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .Add("Error saving {ConfigurationFilePath:l} on {MachineName:l}: {Message:l}", filePath);
            }
        }

        private void StartListeners(bool isInteractive = false)
        {
            foreach (var listener in _eventLogListeners) listener.Start(isInteractive);
        }

        private void StopListeners()
        {
            foreach (var listener in _eventLogListeners) listener.Stop();
        }
    }
}