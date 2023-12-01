using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, IDictionary<string, string> properties, byte[] payloadBytes)
   {
      return payloadBytes;
   }
}