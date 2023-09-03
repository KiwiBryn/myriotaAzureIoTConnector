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
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Functions.Worker;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    public class MyriotaUplinkMessageProcessor
    {
        private static ILogger _logger;
        private static Models.AzureIoT _AzureIoTSettings;
        private static IAzureDeviceClientCache _azuredeviceClientCache;


        public MyriotaUplinkMessageProcessor(ILoggerFactory loggerFactory, IOptions<Models.AzureIoT> azureIoTSettings, IAzureDeviceClientCache azuredeviceClientCache)
        {
            _logger = loggerFactory.CreateLogger<MyriotaUplinkMessageProcessor>();
            _AzureIoTSettings = azureIoTSettings.Value;
            _azuredeviceClientCache = azuredeviceClientCache;
        }

        [Function("UplinkMessageProcessor")]
        public async Task UplinkMessageProcessor([QueueTrigger("uplink", Connection = "UplinkQueueStorage")] Models.UplinkPayloadQueueDto payload)
        {
            DeviceClient deviceClient;

            using (_logger.BeginScope("MyriotaUplinkMessageProcessor"))
            {
                _logger.LogInformation("Application:{Application} PayloadId:{Id} ReceivedAtUTC:{PayloadReceivedAtUtc:yyyy:MM:dd HH:mm:ss} ArrivedAtUTC:{PayloadArrivedAtUtc:yyyy:MM:dd HH:mm:ss} Endpoint:{EndpointRef} Packets:{Count}", payload.Application, payload.Id, payload.PayloadReceivedAtUtc, payload.PayloadArrivedAtUtc, payload.EndpointRef, payload.Data.Packets.Count);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    foreach (Models.QueuePacket packet in payload.Data.Packets)
                    {
                        _logger.LogDebug("TerminalId:{TerminalId} Timestamp:{Timestamp:yyyy:MM:dd HH:mm:ss}  Value:{Value}", packet.TerminalId, packet.Timestamp, packet.Value);
                    }
                }
            }

            foreach (Models.QueuePacket packet in payload.Data.Packets)
            {
                Dictionary<string, string> properties = new Dictionary<string, string>();

                try
                {
                    deviceClient = await GetOrAddAsync(packet.TerminalId, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Uplink- TerminalId:{TerminalId} PayloadId:{payload.Id} GetOrAddAsync failed", packet.TerminalId, payload.Id);

                    throw;
                }

                JObject telemetryEvent = new JObject();

                telemetryEvent.TryAdd("PayloadReceivedAtUtc", payload.PayloadReceivedAtUtc.ToString("s", CultureInfo.InvariantCulture));
                telemetryEvent.TryAdd("PayloadArrivedAtUtc", payload.PayloadArrivedAtUtc.ToString("s", CultureInfo.InvariantCulture));
                telemetryEvent.TryAdd("Application", payload.Application);
                telemetryEvent.TryAdd("PayloadId", payload.Id);
                telemetryEvent.TryAdd("EndpointReference", payload.EndpointRef);

                telemetryEvent.TryAdd("TerminalId", packet.TerminalId);
                telemetryEvent.TryAdd("PacketArrivedAtUtc", packet.Timestamp.ToString("s", CultureInfo.InvariantCulture));
                telemetryEvent.TryAdd("Value", packet.Value);

                _logger.LogDebug("Uplink-PayloadId:{0} TerminalId:{1} TelemetryEvent:{0}", payload.Id, packet.TerminalId, JsonConvert.SerializeObject(telemetryEvent, Formatting.Indented));

                using (Message ioTHubmessage = new Message(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(telemetryEvent))))
                {
                    // This is so nasty but can't find a better way
                    foreach (var property in properties)
                    {
                        ioTHubmessage.Properties.TryAdd(property.Key, property.Value);
                    }

                    ioTHubmessage.Properties.TryAdd("Application", payload.Application.ToString());
                    ioTHubmessage.Properties.TryAdd("Id", payload.Id.ToString());
                    ioTHubmessage.Properties.TryAdd("EndpointReference", payload.EndpointRef.ToString());
                    ioTHubmessage.Properties.TryAdd("TerminalId", packet.TerminalId.ToString());

                    try
                    {
                        await deviceClient.SendEventAsync(ioTHubmessage);
                    }
                    catch (Exception sex)
                    {
                        _logger.LogWarning(sex, "Uplink- TerminalId:{0} PayloadId:{1} SendEventAsync failed", packet.TerminalId, payload.Id);

                        //await Remove(packet.TerminalId);
                        //await this.Remove(packet.TerminalId);

                        throw;
                    }
                }
            }
        }

        public async Task<DeviceClient> GetOrAddAsync(string terminalId, object context)
        {
            DeviceClient deviceClient = null;

            deviceClient = await _azuredeviceClientCache.GetOrAddAsync(terminalId, (ICacheEntry x) => AzureIoTHubDeviceConnectionStringConnectAsync(terminalId, context));

            return deviceClient;
        }

        private async Task<DeviceClient> AzureIoTHubDeviceConnectionStringConnectAsync(string terminalId, object context)
        {
            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(_AzureIoTSettings.AzureIoTHub.ConnectionString, terminalId, TransportSettings);

            await deviceClient.OpenAsync();

            return deviceClient;
        }

        private static readonly ITransportSettings[] TransportSettings = new ITransportSettings[]
        {
            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
            {
                AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                {
                    Pooling = true,
                }
             }
        };
    }
}
