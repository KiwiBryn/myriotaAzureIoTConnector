// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models;

 public class UplinkPayloadQueueDto
 {
     public string EndpointRef { get; set; }
     public DateTime PayloadReceivedAtUtc { get; set; }
     public DateTime PayloadArrivedAtUtc { get; set; }
     public QueueData Data { get; set; }
     public string Id { get; set; }
     public Uri CertificateUrl { get; set; }
     public string Signature { get; set; }
 }

 public class QueueData
 {
     public List<QueuePacket> Packets { get; set; }
 }

 public class QueuePacket
 {
     public string TerminalId { get; set; }

     public DateTime Timestamp { get; set; }

     public string Value { get; set; }
 }
