using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
   public JObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte[] payloadBytes)
   {
      JObject telemetryEvent = new JObject();

      properties.Add("iothub-creation-time-utc", timestamp.ToString("s", CultureInfo.InvariantCulture));

      if (payloadBytes is null)
      {
         return telemetryEvent;
      }

      telemetryEvent.Add("PayloadBytes", BitConverter.ToString(payloadBytes));

      return telemetryEvent;
   }
}