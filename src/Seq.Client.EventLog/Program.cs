using System;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;

namespace Seq.Client.EventLog
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// The service can be installed or uninstalled from the command line
        /// by passing the /install or /uninstall argument.
        /// </summary>
        public static void Main(string[] args)
        {
            // Allows the installation and uninstallation via the command line
            if (Environment.UserInteractive)
            {
                var parameter = string.Concat(args);
                switch (parameter)
                {
                    case "/install":
                        ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                        break;
                    case "/uninstall":
                        ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                        break;
                }
            }
            else
            {
                // Run the service
                ServiceBase.Run(new Service());
            }
        }
    }
}
