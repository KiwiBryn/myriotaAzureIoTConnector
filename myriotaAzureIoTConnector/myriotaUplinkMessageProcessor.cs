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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Azure.Functions.Worker;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    public class MyriotaUplinkMessageProcessor
    {
        private static ILogger _logger;
        private static Models.AzureIoT _azureIoTSettings;
        private static IAzureDeviceClientCache _azuredeviceClientCache;
        private readonly IPayloadFormatterCache _payloadFormatterCache;


        public MyriotaUplinkMessageProcessor(ILoggerFactory loggerFactory, IOptions<Models.AzureIoT> azureIoTSettings, IAzureDeviceClientCache azuredeviceClientCache, IPayloadFormatterCache payloadFormatterCache)
        {
            _logger = loggerFactory.CreateLogger<MyriotaUplinkMessageProcessor>();
            _azureIoTSettings = azureIoTSettings.Value;
            _azuredeviceClientCache = azuredeviceClientCache;
            _payloadFormatterCache = payloadFormatterCache;
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

            IFormatterUplink payloadFormatterUplink;

            try
            {
                payloadFormatterUplink = await _payloadFormatterCache.UplinkGetAsync(payload.Application);
            }
            catch (CSScriptLib.CompilerException cex)
            {
                _logger.LogInformation(cex, "Uplink-PayloadID:{Id} Application:{Application} payload formatter compilation failed", payload.Id, payload.Application);

                throw new InvalidProgramException("Uplink payload formatter invalid or not found");
            }

            foreach (Models.QueuePacket packet in payload.Data.Packets)
            {
                Dictionary<string, string> properties = new Dictionary<string, string>();

                deviceClient = await GetOrAddAsync(packet.TerminalId, null);

                byte[] payloadBytes;

                try
                {
                    payloadBytes = Convert.FromHexString(packet.Value);
                }
                catch (FormatException fex)
                {
                    _logger.LogWarning(fex, "Uplink- TerminalId:{0} Payload:{1} Convert.FromBase64String(payload.Data) failed", packet.TerminalId, payload.Id );

                    throw new ArgumentException("Convert.FromBase64String(payload.Data) failed");
                }

                string payloadText = string.Empty;
                JObject payloadJson = null;

                if (payloadBytes.Length > 1)
                {
                    try
                    {
                        payloadText = Encoding.UTF8.GetString(payloadBytes);

                        payloadJson = JObject.Parse(payloadText);
                    }
                    catch (FormatException fex)
                    {
                        _logger.LogInformation(fex, "Uplink- TerminalId:{0} PayloadId:{1} Encoding.UTF8.GetString(payloadBytes) failed", packet.TerminalId, payload.Id);
                    }
                    catch (JsonReaderException jrex)
                    {
                        _logger.LogInformation(jrex, "Uplink- TerminalId:{0} PayloadId:{1} JObject.Parse(payloadText) failed", packet.TerminalId, payload.Id);
                    }
                }

                JObject telemetryEvent = payloadFormatterUplink.Evaluate(properties, payload.Application, packet.TerminalId, packet.Timestamp, payloadJson, payloadText, payloadBytes);

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

                        throw;
                    }
                }
            }
        }

        public async Task<DeviceClient> GetOrAddAsync(string terminalId, object context)
        {
            DeviceClient deviceClient = null;

            switch (_azureIoTSettings.AzureIoTHub.ConnectionType)
            {
                case Models.AzureIotHubConnectionType.DeviceConnectionString:
                    deviceClient = await _azuredeviceClientCache.GetOrAddAsync(terminalId, (ICacheEntry x) => AzureIoTHubDeviceConnectionStringConnectAsync(terminalId, context));
                    break;
                case Models.AzureIotHubConnectionType.DeviceProvisioningService:
                    deviceClient = await _azuredeviceClientCache.GetOrAddAsync(terminalId, (ICacheEntry x) => AzureIoTHubDeviceProvisioningServiceConnectAsync(terminalId, context));
                    break;
                default:
                    _logger.LogError("Azure IoT Hub ConnectionType unknown {0}", _azureIoTSettings.AzureIoTHub.ConnectionType);

                    throw new NotImplementedException("AzureIoT Hub unsupported ConnectionType");
            }

            return deviceClient;
        }

        private async Task<DeviceClient> AzureIoTHubDeviceConnectionStringConnectAsync(string terminalId, object context)
        {

            DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.AzureIoTHub.ConnectionString, terminalId, TransportSettings);

            await deviceClient.OpenAsync();

            return deviceClient;
        }

        private async Task<DeviceClient> AzureIoTHubDeviceProvisioningServiceConnectAsync(string terminalId, object context)
        {
            DeviceClient deviceClient;

            string deviceKey;
            using (var hmac = new HMACSHA256(Convert.FromBase64String(_azureIoTSettings.AzureIoTHub.DeviceProvisioningService.GroupEnrollmentKey)))
            {
                deviceKey = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(terminalId)));
            }

            using (var securityProvider = new SecurityProviderSymmetricKey(terminalId, deviceKey, null))
            {
                using (var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly))
                {
                    DeviceRegistrationResult result;

                    ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
                        _azureIoTSettings.AzureIoTHub.DeviceProvisioningService.GlobalDeviceEndpoint,
                        _azureIoTSettings.AzureIoTHub.DeviceProvisioningService.IdScope,
                        securityProvider,
                        transport);

                    result = await provClient.RegisterAsync();
  
                    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                    {
                        _logger.LogWarning("Uplink-DeviceID:{deviceId} RegisterAsync status:{result.Status} failed ", terminalId, result.Status);

                        throw new ApplicationException($"Uplink-DeviceID:{terminalId} RegisterAsync status:{result.Status} failed");
                    }

                    IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                    deviceClient = DeviceClient.Create(result.AssignedHub, authentication, TransportSettings);
                }
            }

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