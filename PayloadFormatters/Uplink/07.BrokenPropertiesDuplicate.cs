// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;
using System.Collections.Generic;


public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
   public JsonObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte[] payloadBytes)
   {
      JsonObject telemetryEvent = new JsonObject();

      properties.Add("PayloadId", "Id");
      properties.Add("PayloadId", "Id");

      telemetryEvent.Add("Bytes", BitConverter.ToString(payloadBytes));

      return telemetryEvent;
   }
}