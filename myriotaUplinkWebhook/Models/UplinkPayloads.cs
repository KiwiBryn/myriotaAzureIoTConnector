// Copyright (c) August 2024, devMobile Software, MIT License
//
// Used new Visual Studio feature "Edit->Paste Special->Paste JSON as Clases" functionality
//
namespace devMobile.IoT.myriotaAzureIoTConnector.myriota.UplinkWebhook.Models;


public class UplinkPayloadWebDto
{
   public string EndpointRef { get; set; } = string.Empty;
   public ulong Timestamp { get; set; }
   public string Data { get; set; } = string.Empty; // Embedded JSON
   public string Id { get; set; } = string.Empty;
   public string CertificateUrl { get; set; } = string.Empty;
   public string Signature { get; set; } = string.Empty;
}

public class WebData
{
   public List<WebPacket> Packets { get; set; }
}

public class WebPacket
{
   public string TerminalId { get; set; } = string.Empty;

   public ulong Timestamp { get; set; }

   public string Value { get; set; } = string.Empty;
}

public class UplinkPayloadQueueDto
{
   public string EndpointRef { get; set; } = string.Empty;
   public DateTime PayloadReceivedAtUtc { get; set; }
   public DateTime PayloadArrivedAtUtc { get; set; }
   public QueueData Data { get; set; }
   public string Id { get; set; } = string.Empty;
   public Uri CertificateUrl { get; set; }
   public string Signature { get; set; } = string.Empty;
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
