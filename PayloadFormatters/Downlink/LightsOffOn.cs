using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(IDictionary<string, string> properties, string terminalId, JObject payloadJson, byte[] payloadBytes)
   {
      bool? light = payloadJson.Value<bool?>("Light");

      if (!light.HasValue)
      {
         return new byte[] { };
      }

      return new byte[] { Convert.ToByte(light.Value) };
   }
}