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
                    .Add("{AppName:l} v{AppVersion:l} Starting in interactive mode on {MachineName:l} ...",
                        Config.AppName, Config.AppVersion);

                var client = new EventLogClient();
                client.Start(true, configFilePath);
                ServiceManager.Start(true);

                var done = new ManualResetEvent(false);
                Console.CancelKeyPress += (s, e) =>
                {
                    Log.Debug().Add("Ctrl+C pressed, stopping");
                    client.Stop(configFilePath);
                    done.Set();
                };

                done.WaitOne();
                ServiceManager.Stop();
                Log.Debug()
                    .Add("{AppName:l} v{AppVersion:l} Stopped in interactive  mode on {MachineName:l}", Config.AppName,
                        Config.AppVersion);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .Add("An unhandled exception occurred on {MachineName:l}: {Message:l}");
                Environment.ExitCode = 1;
            }
            finally
            {
                Logging.Close();
            }
        }

        private static void RunService()
        {
            var logFile = string.Empty;
            if (Config.LogToFile)
            {
                var logFolder = Config.LogFolder;

                if (string.IsNullOrEmpty(logFolder))
                    logFolder = Path.Combine(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Logs");

                if (!Directory.Exists(logFolder))
                    Directory.CreateDirectory(logFolder);

                logFile = Path.Combine(logFolder ?? string.Empty, "ServiceLog.txt");

                Logging.SetConfig(new LoggingConfig(appName: Config.AppName, appVersion: Config.AppVersion,
                    logType: new List<LogType> { LogType.File, LogType.Seq }, logDays: 7, logName: Config.AppName,
                    logFolder: Config.LogFolder, logSeqServer: Config.SeqServer, logSeqApiKey: Config.SeqApiKey,
                    logLevel: LurgLevel.Verbose, logLevelFile: LurgLevel.Verbose, logLevelSeq: LurgLevel.Verbose));
            }
            else
            {
                Logging.SetConfig(new LoggingConfig(appName: Config.AppName, appVersion: Config.AppVersion,
                    logType: new List<LogType> { LogType.Seq }, logSeqServer: Config.SeqServer,
                    logSeqApiKey: Config.SeqApiKey,
                    logLevel: LurgLevel.Verbose, logLevelSeq: LurgLevel.Verbose));
            }

            try
            {
                Log.Debug()
                    .Add("{AppName:l} v{AppVersion:l} Starting as service on {MachineName:l} ...", Config.AppName,
                        Config.AppVersion);
                Log.Debug()
                    .AddProperty("LogFolder", Config.LogFolder, false, false)
                    .AddProperty("LogPath", logFile, false, false)
                    .AddProperty("SeqServer", Config.SeqServer)
                    .AddProperty("SeqApiKey", !string.IsNullOrEmpty(Config.SeqApiKey))
                    .Add(Config.LogToFile
                        ? "{AppName:l} ({MachineName:l}) Log Config - LogFolder: {LogFolder:l}, LogPath: {LogPath:l}, Seq Server: {SeqServer:l}, Api Key: {SeqApiKey}"
                        : "{AppName:l} ({MachineName:l}) Log Config - Seq Server: {SeqServer:l}, Api Key: {SeqApiKey}");
                Log.Debug().Add("Running as service");
                ServiceManager.Start(false);
                ServiceBase.Run(new Service());
                ServiceManager.Stop();
                Log.Debug()
                    .Add("{AppName:l} v{AppVersion:l} Stopped as service on {MachineName:l}", Config.AppName,
                        Config.AppVersion);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).AddProperty("Message", ex.Message)
                    .Add("Exception thrown from service host on {MachineName:l}: {Message:l}");
            }
            finally
            {
                Logging.Close();
            }
        }
    }
}