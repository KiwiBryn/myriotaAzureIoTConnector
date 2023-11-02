using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(IDictionary<string, string> properties, string terminalId, JObject payloadJson, byte[] payloadBytes)
   {
      byte? status = payloadJson.Value<byte?>("FanSpeed");

      if (!status.HasValue)
      {
         return new byte[] { };
      }

      return new byte[] { 1, status.Value };
   }
}