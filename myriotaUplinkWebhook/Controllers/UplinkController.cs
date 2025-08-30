// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.myriotaAzureIoTConnector.myriota.UplinkWebhook.Controllers;


[Route("[controller]")]
[ApiController]
public class UplinkController(QueueServiceClient queueServiceClient, ILogger<UplinkController> logger) : ControllerBase
{
   [HttpPost()]
   public async Task<IActionResult> Post([FromBody] Models.UplinkPayloadWebDto payloadWeb)
   {
      // included payload ID for correlation as uplink message processed
      logger.LogInformation("Uplink- Payload ID:{Id}", payloadWeb.Id);

      try
      {
         // Could of used AutoMapper but didn't seem worth it for one place
         Models.UplinkPayloadQueueDto payloadQueue = new Models.UplinkPayloadQueueDto
         {
            EndpointRef = payloadWeb.EndpointRef,
            PayloadReceivedAtUtc = DateTime.UnixEpoch.AddSeconds(payloadWeb.Timestamp),
            PayloadArrivedAtUtc = DateTime.UtcNow,
            Id = payloadWeb.Id,
            Data = new Models.QueueData
            {
               Packets = new System.Collections.Generic.List<Models.QueuePacket>()
            },
            CertificateUrl = new Uri(payloadWeb.CertificateUrl),
            Signature = payloadWeb.Signature
         };

         Models.WebData webData;

         // special case for broken payload in payloadWeb.Data
         try
         {
            webData = JsonSerializer.Deserialize<Models.WebData>(payloadWeb.Data);
         }
         catch (JsonException jex)
         {
            logger.LogError(jex, "UplinkController.Post JsonException Deserialising payloadWeb.Data");

            return this.BadRequest("JsonException Deserialising payloadWeb.Data");
         }

         foreach (var packet in webData.Packets)
         {
            payloadQueue.Data.Packets.Add(new Models.QueuePacket()
            {
               TerminalId = packet.TerminalId,
               Timestamp = DateTime.UnixEpoch.AddMilliseconds(packet.Timestamp),
               Value = packet.Value
            });

            logger.LogInformation("Uplink- TerminalId:{TerminalId} PayloadId:{Id} TelemetryEvent:{telemetryEvent} Sending", packet.TerminalId, payloadWeb.Id, JsonSerializer.Serialize(packet));
         }

         QueueClient queueClient = queueServiceClient.GetQueueClient("uplink");

         await queueClient.SendMessageAsync(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payloadQueue)));

         // included payload ID for correlation as uplink message processed
         logger.LogInformation("SendAsync payload ID:{Id} sent", payloadWeb.Id);
      }
      catch (Exception ex)
      {
         logger.LogError(ex, "Uplink- Controller POST failed");

         return this.StatusCode(500);
      }

      return this.Ok();
   }
}