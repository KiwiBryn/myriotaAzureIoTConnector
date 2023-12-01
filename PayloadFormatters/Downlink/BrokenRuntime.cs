using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, IDictionary<string, string> properties, JObject payloadJson, byte[] payloadBytes)
   {
      payloadBytes[20] = 0;

      return payloadBytes;
   }
}