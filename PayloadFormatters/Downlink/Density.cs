// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonObject payloadJson, byte[] payloadBytes)
   {
      float? front = (float)payloadJson["Front"].GetValue<double>();
      float? middle = (float)payloadJson["Middle"].GetValue<double>();
      float? back = (float)payloadJson["Back"].GetValue<double>();

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