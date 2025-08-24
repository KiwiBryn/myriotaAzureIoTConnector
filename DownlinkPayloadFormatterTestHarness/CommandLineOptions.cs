// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.myriotaAzureIoTConnector.DownlinkPayloadFormatterTestHarness
{
    public class CommandLineOptions
    {
        [Option('f', "Formatter", Required = true, HelpText = "payload formatter path")]
        public string FormatterPath { get; set; }

        [Option('t', "TerminalId", Required = true, HelpText = "Terminal unique identitifer")]
        public string TerminalId { get; set; }

        [Option('m', "MethodName", Required = true, HelpText = "Method name")]
        public string MethodName { get; set; }

        [Option('j', "JSONFile", Required = false, HelpText = "JSON payload file path")]
        public string JsonPayloadPath { get; set; }

        [Option('h', "PayloadHex", Required = false, HelpText = "Hex payload")]
        public string PayloadHex { get; set; }
    }
}