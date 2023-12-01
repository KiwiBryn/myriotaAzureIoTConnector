using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, IDictionary<string, string> properties, JObject payloadJson, byte[] payloadBytes)
   {
      double? temperature = payloadJson.Value<double?>("TemperatureTarget");

      if (!temperature.HasValue)
      {
         return new byte[] { };
      }

      byte[] result = new byte[9];

      result[0] = 2;

      BitConverter.GetBytes(temperature.Value).CopyTo(result, 1);

      return result;
   }
}