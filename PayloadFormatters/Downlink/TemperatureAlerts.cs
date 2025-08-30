// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonObject payloadJson, byte[] payloadBytes)
   {
      // Case sensitive ?
      double? minimum = payloadJson["Minimum"].GetValue<double?>();

      double? maximum = payloadJson["Maximum"].GetValue<double?>();

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