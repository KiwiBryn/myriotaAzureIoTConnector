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
using System.Text.Json;
using System.Threading.Tasks;

using Azure.Storage.Queues;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace devMobile.IoT.myriotaAzureIoTConnector.myriota.UplinkWebhook.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class UplinkController : ControllerBase
    {
        private readonly Models.ApplicationSettings _applicationSettings;
        private readonly ILogger<UplinkController> _logger;
        private readonly QueueServiceClient _queueServiceClient;

        public UplinkController(IOptions<Models.ApplicationSettings> applicationSettings, QueueServiceClient queueServiceClient, ILogger<UplinkController> logger)
        {
            _applicationSettings = applicationSettings.Value;
            _queueServiceClient = queueServiceClient;
            _logger = logger;
        }

        [HttpPost("{application}")]
        public async Task<IActionResult> Post([FromRoute]string application, [FromBody] Models.UplinkPayloadWebDto payloadWeb)
        {
            try
            {
                // Could of used AutoMapper but didn't seem worth it for one place
                Models.UplinkPayloadQueueDto payloadQueue = new Models.UplinkPayloadQueueDto
                {
                    Application = application,
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
                catch( JsonException jex)
                {                     
                    _logger.LogError(jex, "UplinkController.Post JsonException Deserialising payloadWeb.Data");

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
                }

                // included payload ID for correlation as uplink message processed
                _logger.LogInformation("SendAsync queue name:{QueueName} payload ID:{Id}", _applicationSettings.QueueName, payloadWeb.Id);

                QueueClient queueClient = _queueServiceClient.GetQueueClient(_applicationSettings.QueueName);

                await queueClient.SendMessageAsync(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payloadQueue)));
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "UplinkController.Post failed");

                return this.StatusCode(500);
            }

            return this.Ok();
        }
    }
}