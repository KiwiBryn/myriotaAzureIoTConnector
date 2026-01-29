// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models;

 public class MyriotaSettings
 {
     public string BaseUrl { get; set; } = string.Empty;

     public string ApiToken { get; set; } = string.Empty;

     public bool DownlinkEnabled { get; set; } = false;
 }
