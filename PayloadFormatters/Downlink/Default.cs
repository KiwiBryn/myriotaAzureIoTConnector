// Copyright (c) October 2023, devMobile Software, MIT License
//
using System;
using System.Text.Json.Nodes;


public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonObject payloadJson, byte[] payloadBytes)
   {
      return payloadBytes;
   }
}