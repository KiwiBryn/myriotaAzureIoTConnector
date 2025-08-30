// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;
using System.Text.Json.Nodes;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonObject payloadJson, byte[] payloadBytes)
   {
      byte? status = (float)payloadJson["FanSpeed"].GetValue<byte?>();

      if (!status.HasValue)
      {
         return new byte[] { };
      }

      return new byte[] { 1, status.Value };
   }
}