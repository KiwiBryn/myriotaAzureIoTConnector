// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.myriotaAzureIoTConnector.UplinkPayloadFormatterTestHarness;

public class CommandLineOptions
{
   [Option('f', "Formatter", Required = true, HelpText = "payload formatter path")]
   public string FormatterPath { get; set; } = string.Empty;

   [Option('t', "TerminalId", Required = true, HelpText = "Terminal unique identitifer")]
   public string TerminalId { get; set; } = string.Empty;

   [Option('T', "Timestamp", Required = false, HelpText = "Payload packet UTC timestamp(optional, defaults to current)")]
   public DateTime? TimeStamp { get; set; }

   [Option('h', "PayloadHex", Required = true, HelpText = "Hex payload")]
   public string PayloadHex { get; set; } = string.Empty;
}