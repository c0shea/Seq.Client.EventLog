using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using Newtonsoft.Json;

namespace Seq.Client.EventLog
{
    public partial class Service : ServiceBase
    {
        private List<EventLogListener> _eventLogListeners;

        #region Windows Service Base
        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            LoadListeners();
            ValidateListeners();
            StartListeners();
        }
        
        protected override void OnStop()
        {
            StopListeners();
        }
        #endregion

        private void LoadListeners()
        {
            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var filePath = Path.Combine(directory, "EventLogListeners.json");
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
