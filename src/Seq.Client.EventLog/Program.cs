using System;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using Serilog;

namespace Seq.Client.EventLog
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// The service can be installed or uninstalled from the command line
        /// by passing the /install or /uninstall argument, and can be run
        /// interactively by specifying the path to the JSON configuration file.
        /// </summary>
        public static void Main(string[] args)
        {
            // Allows the installation and uninstallation via the command line
            if (Environment.UserInteractive)
            {
                var parameter = string.Concat(args);
                if (string.IsNullOrWhiteSpace(parameter))
                {
                    parameter = null;
                }

                switch (parameter)
                {
                    case "/install":
                        ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                        break;
                    case "/uninstall":
                        ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                        break;
                    default:
                        RunInteractive(parameter);
                        break;
                }
            }
            else
            {
                RunService();
            }
        }

        static void RunInteractive(string configFilePath)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                Log.Information("Running interactively");

                var client = new EventLogClient();
                client.Start(configFilePath);

                var done = new ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) =>
                {
                    Log.Information("Ctrl+C pressed, stopping");
                    client.Stop();
                    done.Set();
                };

                done.WaitOne();
                Log.Information("Stopped");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "An unhandled exception occurred");
                Environment.ExitCode = 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static void RunService()
        {
            ServiceBase.Run(new Service());
        }
    }
}
