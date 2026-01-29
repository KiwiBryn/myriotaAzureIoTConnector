// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models;

 public class PayloadformatterSettings
 {
     public string UplinkContainer { get; set; } = string.Empty;
     public string UplinkFormatterDefault { get; set; } = string.Empty;

     public string DownlinkContainer { get; set; } = string.Empty;
     public string DownlinkFormatterDefault { get; set; } = string.Empty;
 }
