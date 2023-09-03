using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
    public JObject Evaluate(IDictionary<string, string> properties, string application, string terminalId, DateTime timestamp, JObject payloadJson, string payloadText, byte[] payloadBytes)
    {
        JObject telemetryEvent = new JObject();

        telemetryEvent.Add("ASCII", payloadText);
        telemetryEvent.Add("Bits", BitConverter.ToString(payloadBytes));

        return telemetryEvent;
    }
}