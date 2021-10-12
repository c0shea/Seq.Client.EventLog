using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using Lurgle.Logging;

namespace Seq.Client.EventLog
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        ///     The service can be installed or uninstalled from the command line
        ///     by passing the /install or /uninstall argument, and can be run
        ///     interactively by specifying the path to the JSON configuration file.
        /// </summary>
        public static void Main(string[] args)
        {
            // Allows the installation and uninstallation via the command line
            if (Environment.UserInteractive)
            {
                var parameter = string.Concat(args);
                if (string.IsNullOrWhiteSpace(parameter)) parameter = null;

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

        private static void RunInteractive(string configFilePath)
        {
            Logging.SetConfig(new LoggingConfig(appName: Config.AppName, appVersion: Config.AppVersion,
                logType: new List<LogType> { LogType.Console, LogType.Seq }, logSeqServer: Config.SeqServer,
                logSeqApiKey: Config.SeqApiKey, logLevel: LurgLevel.Verbose, logLevelConsole: LurgLevel.Verbose,
                logLevelSeq: LurgLevel.Verbose));

            try
            {
                Log.Debug()
                    .Add("{AppName:l} v{AppVersion:l} Starting ...", Config.AppName, Config.AppVersion);
                Log.Debug().Add("Running interactively");

                var client = new EventLogClient();
                client.Start(true, configFilePath);
                ServiceManager.Start(true);

                var done = new ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) =>
                {
                    Log.Debug().Add("Ctrl+C pressed, stopping");
                    client.Stop();
                    done.Set();
                };

                done.WaitOne();
                ServiceManager.Stop();
                Log.Debug()
                    .Add("{AppName:l} v{AppVersion:l} Stopped", Config.AppName, Config.AppVersion);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("An unhandled exception occurred");
                Environment.ExitCode = 1;
            }
            finally
            {
                Logging.Close();
            }
        }

        private static void RunService()
        {
            var logFolder = Config.LogFolder;

            if (string.IsNullOrEmpty(logFolder))
                logFolder = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Logs");

            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            var logFile = Path.Combine(logFolder ?? string.Empty, "ServiceLog.txt");
            Logging.SetConfig(new LoggingConfig(appName: Config.AppName, appVersion: Config.AppVersion,
                logType: new List<LogType> { LogType.File, LogType.Seq }, logDays: 7, logName: Config.AppName,
                logFolder: Config.LogFolder, logSeqServer: Config.SeqServer, logSeqApiKey: Config.SeqApiKey,
                logLevel: LurgLevel.Verbose, logLevelFile: LurgLevel.Verbose, logLevelSeq: LurgLevel.Verbose));

            try
            {
                Log.Debug()
                    .Add("{AppName:l} v{AppVersion:l} Starting ...", Config.AppName, Config.AppVersion);
                Log.Debug()
                    .Add(
                        "LogFolder: {LogFolder:l}, LogPath: {LogPath:l}, Seq Server: {SeqServer:l}, Api Key: {ApiKeySet}",
                        Config.LogFolder, logFile, Config.SeqServer, !string.IsNullOrEmpty(Config.SeqApiKey));
                Log.Debug().Add("Running as service");
                ServiceManager.Start(false);
                ServiceBase.Run(new Service());
                ServiceManager.Stop();
                Log.Debug()
                    .Add("{AppName:l} v{AppVersion:l} Stopped", Config.AppName, Config.AppVersion);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add("Exception thrown from service host: {Message:l}", ex.Message);
            }
            finally
            {
                Logging.Close();
            }
        }
    }
}