using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, JObject payloadJson, byte[] payloadBytes)
   {
      float x = payloadJson.Value<float>("x");
      float y = payloadJson.Value<float>("y");
      float z = payloadJson.Value<float>("z");

      byte[] result = new byte[13];

      result[0] = 4;

      BitConverter.GetBytes(x).CopyTo(result, 1);
      BitConverter.GetBytes(y).CopyTo(result, 5);
      BitConverter.GetBytes(x).CopyTo(result, 9);

      return result;
   }
}