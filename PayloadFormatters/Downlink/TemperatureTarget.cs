// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JObject payloadJson, byte[] payloadBytes)
   {
      double? temperature = payloadJson["TemperatureTarget"].GetValue<double?>();

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