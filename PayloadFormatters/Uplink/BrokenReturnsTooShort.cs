﻿using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
    public JObject Evaluate(IDictionary<string, string> properties, string terminalId, DateTime timestamp, byte[] payloadBytes)
    {
        JObject telemetryEvent = new JObject();

        telemetryEvent.Add("Bytes", BitConverter.ToString(payloadBytes));

        return telemetryEvent;
    }
}