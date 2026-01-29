// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models;

public class DeviceConnectionContext
{
   public string TerminalId { get; set; } = string.Empty;

   public string ModuleType { get; set; } = string.Empty;

   public string PayloadFormatterUplink { get; set; } = string.Empty;

   public string PayloadFormatterDownlink { get; set; } = string.Empty;

   public Dictionary<string, string> Attibutes { get; set; }

   public DeviceClient DeviceClient { get; set; }
}
