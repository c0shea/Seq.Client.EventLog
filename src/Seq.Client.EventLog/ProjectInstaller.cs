using System.ComponentModel;
using System.Configuration.Install;

namespace Seq.Client.EventLog
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            InitializeComponent();
        }
    }
}