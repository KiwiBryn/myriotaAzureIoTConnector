// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
    public JsonObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte payloadBytes)
    {
        JsonObject telemetryEvent = new JsonObject();

        telemetryEvent.Add("Bytes", BitConverter.ToString(payloadBytes));

        return telemetryEvent;
    }
}