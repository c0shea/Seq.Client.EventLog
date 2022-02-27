using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace Seq.Client.EventLog
{
    public static class Config
    {
        static Config()
        {
            AppName = ConfigurationManager.AppSettings["AppName"];
            SeqServer = ConfigurationManager.AppSettings["LogSeqServer"];
            SeqApiKey = ConfigurationManager.AppSettings["LogSeqApiKey"];
            LogToFile = GetBool(ConfigurationManager.AppSettings["LogSeqApiKey"], true);
            LogFolder = ConfigurationManager.AppSettings["LogFolder"];
            HeartbeatInterval = GetInt(ConfigurationManager.AppSettings["HeartbeatInterval"]);
            HeartbeatsBeforeReset = GetInt(ConfigurationManager.AppSettings["HeartbeatsBeforeReset"]);

            //Minimum is 0 (disabled)
            if (HeartbeatInterval < 0)
                HeartbeatInterval = 600;
            //Maximum is 3600
            if (HeartbeatInterval > 3600)
                HeartbeatInterval = 3600;

            if (HeartbeatsBeforeReset < 0)
                HeartbeatsBeforeReset = 0;

            IsDebug = GetBool(ConfigurationManager.AppSettings["IsDebug"]);

            var isSuccess = true;
            try
            {
                if (string.IsNullOrEmpty(AppName))
                    AppName = Assembly.GetEntryAssembly()?.GetName().Name;

                AppVersion = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
                if (string.IsNullOrEmpty(LogFolder))
                    LogFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            }
            catch
            {
                isSuccess = false;
            }

            if (isSuccess) return;
            try
            {
                if (string.IsNullOrEmpty(AppName))
                    AppName = Assembly.GetExecutingAssembly().GetName().Name;

                AppVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                if (string.IsNullOrEmpty(LogFolder))
                    LogFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
            }
            catch
            {
                //We surrender ...
                AppVersion = string.Empty;
            }
        }

        public static string AppName { get; }
        public static string AppVersion { get; }
        public static string SeqServer { get; }
        public static string SeqApiKey { get; }
        public static bool LogToFile { get; }
        public static string LogFolder { get; }
        public static int HeartbeatInterval { get; }
        public static bool IsDebug { get; }
        public static int HeartbeatsBeforeReset { get; }

        /// <summary>
        ///     Convert the supplied <see cref="object" /> to an <see cref="int" />
        ///     <para />
        ///     This will filter out nulls that could otherwise cause exceptions
        /// </summary>
        /// <param name="sourceObject">An object that can be converted to an int</param>
        /// <returns></returns>
        public static int GetInt(object sourceObject)
        {
            var sourceString = string.Empty;

            if (!Convert.IsDBNull(sourceObject)) sourceString = (string) sourceObject;

            if (int.TryParse(sourceString, out var destInt)) return destInt;

            return -1;
        }

        /// <summary>
        ///     Convert the supplied <see cref="object" /> to a <see cref="bool" />
        ///     <para />
        ///     This will filter out nulls that could otherwise cause exceptions
        /// </summary>
        /// <param name="sourceObject">An object that can be converted to a bool</param>
        /// <param name="trueIfEmpty">Return true if the object is empty</param>
        /// <returns></returns>
        private static bool GetBool(object sourceObject, bool trueIfEmpty = false)
        {
            var sourceString = string.Empty;

            if (!Convert.IsDBNull(sourceObject)) sourceString = (string) sourceObject;

            return bool.TryParse(sourceString, out var destBool) ? destBool : trueIfEmpty;
        }
    }
}