using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
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
            LogFolder = ConfigurationManager.AppSettings["LogFolder"];
            HeartbeatInterval = GetInt(ConfigurationManager.AppSettings["HeartbeatInterval"]);
            IsDebug = GetBool(ConfigurationManager.AppSettings["IsDebug"]);
            ProjectKey = ConfigurationManager.AppSettings["ProjectKey"];
            Responders = ConfigurationManager.AppSettings["Responders"];
            Priority = ConfigurationManager.AppSettings["Priority"];
            Tags = GetArray(ConfigurationManager.AppSettings["Tags"]);
            InitialTimeEstimate = ConfigurationManager.AppSettings["InitialTimeEstimate"];
            RemainingTimeEstimate = ConfigurationManager.AppSettings["RemainingTimeEstimate"];
            DueDate = ConfigurationManager.AppSettings["DueDate"];

            //Must be between 0 and 1 hour in seconds
            if (HeartbeatInterval < 0 || HeartbeatInterval > 3600)
                HeartbeatInterval = 600000;
            else
                HeartbeatInterval *= 1000;

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
        public static string LogFolder { get; }
        public static int HeartbeatInterval { get; }
        public static bool IsDebug { get; }
        public static string ProjectKey { get; }
        public static string Priority { get; }
        public static string Responders { get; }
        public static IEnumerable<string> Tags { get; }
        public static string InitialTimeEstimate { get; }
        public static string RemainingTimeEstimate { get; }
        public static string DueDate { get; }

        /// <summary>
        ///     Convert the supplied <see cref="object" /> to an <see cref="int" />
        ///     <para />
        ///     This will filter out nulls that could otherwise cause exceptions
        /// </summary>
        /// <param name="sourceObject">An object that can be converted to an int</param>
        /// <returns></returns>
        private static int GetInt(object sourceObject)
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

        private static IEnumerable<string> GetArray(string value)
        {
            return (value ?? "")
                .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
        }
    }
}