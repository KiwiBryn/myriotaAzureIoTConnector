// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace PayloadFormatter; // Additional namespace for shortening interface when usage in formatter code

public interface IFormatterUplink
{
   public JsonObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte[] payloadBytes);
}

public interface IFormatterDownlink
{
   public byte[] Evaluate(string terminalId, string methodName, JsonObject payloadJson, byte[] payloadBytes);
}