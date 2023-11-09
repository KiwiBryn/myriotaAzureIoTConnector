// Copyright (c) August 2023, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//---------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
   public class MyriotaUplinkMessageProcessor
   {
      private readonly ILogger _logger;
      private readonly IDeviceConnectionCache _deviceConnectionCache;
      private readonly IPayloadFormatterCache _payloadFormatterCache;

      public MyriotaUplinkMessageProcessor(ILoggerFactory loggerFactory, IDeviceConnectionCache deviceConnectionCache, IPayloadFormatterCache payloadFormatterCache)
      {
         _logger = loggerFactory.CreateLogger<MyriotaUplinkMessageProcessor>();
         _deviceConnectionCache = deviceConnectionCache;
         _payloadFormatterCache = payloadFormatterCache;
      }

      [Function("UplinkMessageProcessor")]
      public async Task UplinkMessageProcessor([QueueTrigger(queueName: "%UplinkQueueName%", Connection = "UplinkQueueStorage")] Models.UplinkPayloadQueueDto payload, CancellationToken cancellationToken)
      {
         _logger.LogInformation("Uplink- PayloadId:{0} ReceivedAtUTC:{1:yyyy:MM:dd HH:mm:ss} ArrivedAtUTC:{2:yyyy:MM:dd HH:mm:ss} Endpoint:{3} Packets:{4}", payload.Id, payload.PayloadReceivedAtUtc, payload.PayloadArrivedAtUtc, payload.EndpointRef, payload.Data.Packets.Count);

         if (_logger.IsEnabled(LogLevel.Debug))
         {
            foreach (Models.QueuePacket packet in payload.Data.Packets)
            {
               _logger.LogDebug("Uplink- PayloadId:{0} TerminalId:{1} Timestamp:{2:yyyy:MM:dd HH:mm:ss} Value:{3}", payload.Id, packet.TerminalId, packet.Timestamp, packet.Value);
            }
         }

         // Process each packet in the payload. Myriota docs say only one packet per payload but just incase...
         foreach (Models.QueuePacket packet in payload.Data.Packets)
         {
            try
            {
               // Convert Hex payload to bytes, if this fails packet broken
               byte[] payloadBytes = Convert.FromHexString(packet.Value);

               Models.DeviceConnectionContext context = await _deviceConnectionCache.GetOrAddAsync(packet.TerminalId, cancellationToken);

               // This shouldn't fail, but it could for lots of different reasons, invalid path to blob, syntax error, interface broken etc.
               IFormatterUplink formatter = await _payloadFormatterCache.UplinkGetAsync(context.PayloadFormatterUplink, cancellationToken);

               Dictionary<string, string> properties = new Dictionary<string, string>();

               // This shouldn't fail, but it could for lots of different reasons, null references, divide by zero, out of range etc.
               JObject telemetryEvent = formatter.Evaluate(properties, packet.TerminalId, packet.Timestamp, payloadBytes);
               if (telemetryEvent is null)
               {
                  _logger.LogError("Uplink- PayloadId:{0} TerminalId:{1} Value:{2} Bytes:{3} payload formatter evaluate failed returned null", payload.Id, packet.TerminalId, packet.Value, Convert.ToHexString(payloadBytes));

                  throw new NullReferenceException("Payload formatter.exvaluate returned null");
               }

               // Enrich the telemetry event with metadata, using TryAdd as some of the values may have been added by the formatter
               telemetryEvent.TryAdd("PayloadId", payload.Id);
               telemetryEvent.TryAdd("EndpointReference", payload.EndpointRef);
               telemetryEvent.TryAdd("TerminalId", packet.TerminalId);
               telemetryEvent.TryAdd("PacketArrivedAtUtc", packet.Timestamp.ToString("s", CultureInfo.InvariantCulture));
               telemetryEvent.TryAdd("PayloadReceivedAtUtc", payload.PayloadReceivedAtUtc.ToString("s", CultureInfo.InvariantCulture));
               telemetryEvent.TryAdd("PayloadArrivedAtUtc", payload.PayloadArrivedAtUtc.ToString("s", CultureInfo.InvariantCulture));

               if (_logger.IsEnabled(LogLevel.Debug))
               {
                  _logger.LogDebug("Uplink- PayloadId:{0} TerminalId:{1} TelemetryEvent:{2}", payload.Id, packet.TerminalId, JsonConvert.SerializeObject(telemetryEvent, Formatting.Indented));
               }

               using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryEvent))))
               {
                  // This is so nasty but can't find a better way
                  foreach (var property in properties)
                  {
                     ioTHubmessage.Properties.TryAdd(property.Key, property.Value);
                  }

                  // Populate the message properties, using TryAdd as some of the properties may have been added by the formatter
                  ioTHubmessage.Properties.TryAdd("PayloadId", payload.Id);
                  ioTHubmessage.Properties.TryAdd("EndpointReference", payload.EndpointRef);
                  ioTHubmessage.Properties.TryAdd("TerminalId", packet.TerminalId);

                  await context.DeviceClient.SendEventAsync(ioTHubmessage, cancellationToken);
               }
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "Uplink- PayloadId:{Id} TerminalId:{TerminalId}", payload.Id, packet.TerminalId);

               throw;
            }
         }
      }
   }
}
