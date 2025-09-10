// Copyright (c) October 2023, devMobile Software, MIT License
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;


public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
   public JsonObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte[] payloadBytes)
   {
      JsonObject telemetryEvent = new JsonObject();

      properties.Add("iothub-creation-time-utc", timestamp.ToString("s", CultureInfo.InvariantCulture));

      if (payloadBytes is null)
      {
         return telemetryEvent;
      }

      telemetryEvent.Add("PayloadBytes", BitConverter.ToString(payloadBytes));

      return telemetryEvent;
   }
}