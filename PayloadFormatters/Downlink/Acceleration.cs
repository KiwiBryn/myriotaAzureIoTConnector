// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;
using System.Text.Json.Nodes;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonObject payloadJson, byte[] payloadBytes)
   {
      float x = (float)payloadJson["x"].GetValue<double>();
      float y = (float)payloadJson["y"].GetValue<double>();
      float z = (float)payloadJson["z"].GetValue<double>();

      byte[] result = new byte[13];

      result[0] = 4;

      BitConverter.GetBytes(x).CopyTo(result, 1);
      BitConverter.GetBytes(y).CopyTo(result, 5);
      BitConverter.GetBytes(x).CopyTo(result, 9);

      return result;
   }
}