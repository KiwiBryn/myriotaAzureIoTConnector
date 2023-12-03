using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, JObject payloadJson, byte[] payloadBytes)
   {
      payloadBytes[20] = 0;

      return payloadBytes;
   }
}