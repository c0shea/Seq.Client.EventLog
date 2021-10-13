using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Xml.Linq;
using Lurgle.Logging;

namespace Seq.Client.EventLog
{
    public static class Extensions
    {
        public static LurgLevel MapLogLevel(EventRecord entry)
        {
            if (entry.Level == null && entry.Keywords == null)
                return LurgLevel.Debug;

            if (entry.Keywords != null)
            {
                if (((StandardEventKeywords)entry.Keywords).HasFlag(StandardEventKeywords.AuditSuccess))
                    return LurgLevel.Information;

                if (((StandardEventKeywords)entry.Keywords).HasFlag(StandardEventKeywords.AuditFailure))
                    return LurgLevel.Warning;
            }

            // ReSharper disable once PossibleInvalidOperationException
            switch ((byte)entry.Level)
            {
                case (byte)EventLogEntryType.Information:
                    return LurgLevel.Information;
                case (byte)EventLogEntryType.Warning:
                    return LurgLevel.Warning;
                case (byte)EventLogEntryType.Error:
                    return LurgLevel.Error;
                case (byte)EventLogEntryType.SuccessAudit:
                    return LurgLevel.Information;
                case (byte)EventLogEntryType.FailureAudit:
                    return LurgLevel.Warning;
                default:
                    return LurgLevel.Debug;
            }
        }

        public static IEnumerable<string> GetArray(string value)
        {
            return (value ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
        }

        public static string GetListenerType(string remoteServer)
        {
            if (string.IsNullOrEmpty(remoteServer) || remoteServer == ".")
                return "Local";
            else
                return string.Format($"\\\\{remoteServer}");
        }

        public static Dictionary<string, object> ParseXml(string xml)
        {
            var result = new Dictionary<string, object>();
            if (string.IsNullOrEmpty(xml))
                return result;

            try
            {
                var xmlDoc = XElement.Parse(xml);
                return ProcessNode(xmlDoc);
            }
            catch (Exception ex)
            {
                Log.Exception(ex).Add(ex.Message);
                return new Dictionary<string, object>();
            }
        }

        private static Dictionary<string, object> ProcessNode(XElement element, int depth = 0, string name = null)
        {
            var result = new Dictionary<string, object>();
            var nodeName = !string.IsNullOrEmpty(name) ? name : element.Name.LocalName;

            if (!element.HasElements && !element.IsEmpty)
                result.Add(nodeName, element.Value);
            else
                foreach (var descendant in element.Elements())
                foreach (var node in ProcessNode(descendant, depth + 1,
                    depth > 0 && !nodeName.Equals("System", StringComparison.OrdinalIgnoreCase)
                        ? string.Format($"{nodeName}-{GetName(descendant)}")
                        : GetName(descendant)))
                    result.Add(node.Key, node.Value);

            return result;
        }

        private static string GetName(XElement element)
        {
            if (element.HasAttributes &&
                element.FirstAttribute.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                return element.FirstAttribute.Value;
            return element.Name.LocalName;
        }

        public static string GetMessage(string message)
        {
            return message.Contains(Environment.NewLine) &&
                   !string.IsNullOrEmpty(message.Substring(0,
                       message.IndexOf(Environment.NewLine, StringComparison.Ordinal)))
                ? message.Substring(0, message.IndexOf(Environment.NewLine, StringComparison.Ordinal))
                : message;
        }
    }
}