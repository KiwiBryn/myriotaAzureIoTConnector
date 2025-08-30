// Copyright (c) October 2023, devMobile Software, MIT License
//
using System;
using System.Text.Json;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonDocument payloadJson, byte[] payloadBytes)
   {
      return payloadBytes;
   }
}