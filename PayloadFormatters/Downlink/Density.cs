using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(IDictionary<string, string> properties, string terminalId, JObject payloadJson, byte[] payloadBytes)
   {
      float? front = payloadJson.Value<float>("Front");
      float? middle = payloadJson.Value<float>("Middle");
      float? back = payloadJson.Value<float>("Back");

      byte[] result = new byte[13];

      result[0] = 5;

      if (front.HasValue)
      {
         BitConverter.GetBytes(front.Value).CopyTo(result, 1);
      }

      if (middle.HasValue)
      {
         BitConverter.GetBytes(middle.Value).CopyTo(result, 5);
      }

      if (back.HasValue)
      {
         BitConverter.GetBytes(back.Value).CopyTo(result, 9);
      }

      return result;
   }
}