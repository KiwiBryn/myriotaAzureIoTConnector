using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, JObject payloadJson, byte[] payloadBytes)
   {
      // Case sensitive ?
      double? minimum = payloadJson.Value<double?>("Minimum");

      double? maximum = payloadJson.Value<double?>("Maximum");

      if (!minimum.HasValue || !maximum.HasValue)
      {
         return new byte[] { };
      }

      byte[] result = new byte[17];

      result[0] = 3;

      BitConverter.GetBytes(minimum.Value).CopyTo(result, 1);

      BitConverter.GetBytes(maximum.Value).CopyTo(result, 9);

      return result;
   }
}