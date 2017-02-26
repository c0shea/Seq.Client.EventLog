using System;
using System.Collections.Generic;

namespace Seq.Client.EventLog
{
    public class RawEvent
    {
        public DateTimeOffset Timestamp { get; set; }

        // Uses the Serilog level names
        public string Level { get; set; }

        public string MessageTemplate { get; set; }

        public Dictionary<string, object> Properties { get; set; }

        public string Exception { get; set; }
    }
}
