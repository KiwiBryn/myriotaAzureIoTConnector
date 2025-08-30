// Copyright (c) August 2024, devMobile Software, MIT License
//
// Used new Visual Studio feature "Edit->Paste Special->Paste JSON as Clases" functionality
//
namespace devMobile.IoT.myriotaAzureIoTConnector.myriota.UplinkWebhook.Models;


 public class UplinkPayloadWebDto
 {
     public required string EndpointRef { get; set; }
     public ulong Timestamp { get; set; }
     public required string Data { get; set; } // Embedded JSON
     public required string Id { get; set; }
     public required string CertificateUrl { get; set; }
     public required string Signature { get; set; }
 }

 public class WebData
 {
     public List<WebPacket> Packets { get; set; }
 }

 public class WebPacket
 {
     public required string TerminalId { get; set; }

     public ulong Timestamp { get; set; }

     public required string Value { get; set; }
 }

 public class UplinkPayloadQueueDto
 {
     public required string EndpointRef { get; set; }
     public DateTime PayloadReceivedAtUtc { get; set; }
     public DateTime PayloadArrivedAtUtc { get; set; }
     public QueueData Data { get; set; }
     public string Id { get; set; }
     public Uri CertificateUrl { get; set; }
     public required string Signature { get; set; }
 }

 public class QueueData
 {
     public List<QueuePacket> Packets { get; set; }
 }

 public class QueuePacket
 {
     public required string TerminalId { get; set; }

     public DateTime Timestamp { get; set; }

     public required string Value { get; set; }
 }
