using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterUplink
{
    public JObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte[] payloadBytes)
    {
        JObject telemetryEvent = new JObject();

        telemetryEvent.Add("Bytes", BitConverter.ToString(payloadBytes));

        return telemetryEvent;
    }
}