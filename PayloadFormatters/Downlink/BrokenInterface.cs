// Copyright (c) August 2025, devMobile Software, MIT License
//
using System;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, byte[] payloadBytes)
   {
      return payloadBytes;
   }
}