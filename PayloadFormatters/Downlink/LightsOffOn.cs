// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;
//using System.Collections.Generic;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonObject payloadJson, byte[] payloadBytes)
   {
      bool? light = (bool)payloadJson["Light"].GetValue<bool?>();

      if (!light.HasValue)
      {
         return new byte[] { };
      }

      return new byte[] { 0, Convert.ToByte(light.Value) };
   }
}