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
            // Could of used AutoMapper but didn't seem worth it for one place
            Models.UplinkPayloadQueueDto payloadQueue = new Models.UplinkPayloadQueueDto
            {
                Application = application,  
                EndpointRef = payloadWeb.EndpointRef,
                Timestamp = DateTime.UnixEpoch.AddSeconds(payloadWeb.Timestamp),
                Id = payloadWeb.Id,
                Data = new Models.QueueData
                {
                    Packets = new System.Collections.Generic.List<Models.QueuePacket>()
                },
                Certificate = new Uri(payloadWeb.CertificateUrl),
                Signature = payloadWeb.Signature
            };

            Models.WebData webData = JsonSerializer.Deserialize<Models.WebData>(payloadWeb.Data);

            foreach (var packet in webData.Packets)
            {
                payloadQueue.Data.Packets.Add(new Models.QueuePacket()
                {
                    TerminalId = packet.TerminalId,
                    Timestamp = DateTime.UnixEpoch.AddMilliseconds(packet.Timestamp),
                    Value = packet.Value
                });
            }

            _logger.LogInformation("SendAsync queue name:{QueueName}", _applicationSettings.QueueName);

            QueueClient queueClient = _queueServiceClient.GetQueueClient(_applicationSettings.QueueName);

            await queueClient.SendMessageAsync(Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payloadQueue)));

            return this.Ok();
        }
    }
}