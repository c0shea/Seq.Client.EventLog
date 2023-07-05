using System.ServiceProcess;

namespace Seq.Client.EventLog
{
    public partial class Service : ServiceBase
    {
        #region Windows Service Base

        public Service()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            EventLogClient.Start();
        }

        protected override void OnStop()
        {
            EventLogClient.Stop();
        }

        #endregion
    }
}