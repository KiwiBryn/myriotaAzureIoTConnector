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
using Microsoft.Azure.Devices.Client.Exceptions;
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
        private readonly ILogger _logger;
        private readonly Models.AzureIoT _azureIoTSettings;
        private readonly IAzureDeviceClientCache _azuredeviceClientCache;
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
            _logger.LogInformation("Uplink- PayloadId:{0} Application:{1} ReceivedAtUTC:{2:yyyy:MM:dd HH:mm:ss} ArrivedAtUTC:{3:yyyy:MM:dd HH:mm:ss} Endpoint:{4} Packets:{5}", payload.Id, payload.Application, payload.PayloadReceivedAtUtc, payload.PayloadArrivedAtUtc, payload.EndpointRef, payload.Data.Packets.Count);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                foreach (Models.QueuePacket packet in payload.Data.Packets)
                {
                    _logger.LogDebug("Uplink- PayloadId:{0} TerminalId:{1} Timestamp:{2:yyyy:MM:dd HH:mm:ss} Value:{3}", payload.Id, packet.TerminalId, packet.Timestamp, packet.Value);
                }
            }

            // Get the payload formatter for Application from Azure Storage container, compile, and then cache binary.
            IFormatterUplink payloadFormatterUplink;

            try
            {
                payloadFormatterUplink = await _payloadFormatterCache.UplinkGetAsync(payload.Application);
            }
            catch (CSScriptLib.CompilerException cex)
            {
                _logger.LogError(cex, "Uplink- PayloadID:{0} Application:{1} payload formatter compilation failed", payload.Id, payload.Application);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Uplink- PayloadID:{0} Application:{1} payload formatter load failed", payload.Id, payload.Application);

                throw;
            }

            // Process each packet in the payload. Myriota docs say only one packet per payload but just incase...
            foreach (Models.QueuePacket packet in payload.Data.Packets)
            {
                byte[] payloadBytes;

                // Convert Hex payload to bytes, if this fails packet broken
                try
                {
                    payloadBytes = Convert.FromHexString(packet.Value);
                }
                catch (FormatException fex)
                {
                    _logger.LogError(fex, "Uplink- Payload:{0} TerminalId:{1} Convert.FromHexString({2}) failed", payload.Id, packet.TerminalId, packet.Value);

                    throw;
                }

                string payloadText = string.Empty;

                // Convert bytes to text, if this fails then not a text payload
                try
                {
                    payloadText = Encoding.UTF8.GetString(payloadBytes);
                }
                catch (ArgumentException aex)
                {
                    _logger.LogDebug(aex, "Uplink- PayloadId:{0} TerminalId:{1} Encoding.UTF8.GetString({2}) failed", payload.Id, packet.TerminalId, Convert.ToHexString(payloadBytes));
                }

                JObject? payloadJson = null;

                // Convert text to JSON, if this fails then not a JSON payload
                try
                {
                    payloadJson = JObject.Parse(payloadText);
                }
                catch (JsonReaderException jex)
                {
                    _logger.LogDebug(jex, "Uplink- PayloadId:{0} TerminalId:{1} JObject.Parse({2}) failed", payload.Id, packet.TerminalId, payloadText);
                }

                // Process the payload with application specific formatter
                Dictionary<string, string> properties = new Dictionary<string, string>();
                JObject telemetryEvent;

                try
                {
                    telemetryEvent = payloadFormatterUplink.Evaluate(properties, payload.Application, packet.TerminalId, packet.Timestamp, payloadJson, payloadText, payloadBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Uplink- PayloadId:{0} TerminalId:{1} Value:{2} Bytes:{3} Text:{4} JSON:{5} payload formatter evaluate failed", payload.Id, packet.TerminalId, packet.Value, Convert.ToHexString(payloadBytes), payloadText, payloadJson);

                    throw ;
                }

                if (telemetryEvent is null)
                {
                    _logger.LogError("Uplink- PayloadId:{0} TerminalId:{1} Value:{2} Bytes:{3} Text:{4} JSON:{5} payload formatter evaluate failed returned null", payload.Id, packet.TerminalId, packet.Value, Convert.ToHexString(payloadBytes), payloadText, payloadJson);

                    throw new ArgumentNullException(nameof(telemetryEvent));
                }

                // Enrich the telemetry event with metadata, using TryAdd as some of the values may have been added by the formatter
                telemetryEvent.TryAdd("PayloadId", payload.Id);
                telemetryEvent.TryAdd("Application", payload.Application);
                telemetryEvent.TryAdd("EndpointReference", payload.EndpointRef);
                telemetryEvent.TryAdd("TerminalId", packet.TerminalId);
                telemetryEvent.TryAdd("PacketArrivedAtUtc", packet.Timestamp.ToString("s", CultureInfo.InvariantCulture));
                telemetryEvent.TryAdd("PayloadReceivedAtUtc", payload.PayloadReceivedAtUtc.ToString("s", CultureInfo.InvariantCulture));
                telemetryEvent.TryAdd("PayloadArrivedAtUtc", payload.PayloadArrivedAtUtc.ToString("s", CultureInfo.InvariantCulture));

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Uplink-PayloadId:{0} TerminalId:{1} TelemetryEvent:{2}", payload.Id, packet.TerminalId, JsonConvert.SerializeObject(telemetryEvent, Formatting.Indented));
                }

                // Lookup the device client in the cache or create a new one
                DeviceClient deviceClient;

                try
                {
                    deviceClient = await GetOrAddAsync(packet.TerminalId, null);
                }
                catch (DeviceNotFoundException dnfex)
                {
                    _logger.LogError(dnfex, "Uplink- PayloadId:{0} TerminalId:{1} terminal not found", payload.Id, packet.TerminalId);

                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Uplink- PayloadId:{0} TerminalId:{1} ", payload.Id, packet.TerminalId);

                    throw;
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
                    ioTHubmessage.Properties.TryAdd("Application", payload.Application);
                    ioTHubmessage.Properties.TryAdd("EndpointReference", payload.EndpointRef);
                    ioTHubmessage.Properties.TryAdd("TerminalId", packet.TerminalId);

                    try
                    {
                        await deviceClient.SendEventAsync(ioTHubmessage);
                    }
                    catch (Exception sex)
                    {
                        _logger.LogError(sex, "Uplink- PayloadId:{0} TerminalId:{1} SendEventAsync failed", payload.Id, packet.TerminalId);

                        throw;
                    }
                }
            }
        }

        public async Task<DeviceClient> GetOrAddAsync(string terminalId, object context)
        {
            DeviceClient deviceClient;

            switch (_azureIoTSettings.AzureIoTHub.ConnectionType)
            {
                case Models.AzureIotHubConnectionType.DeviceConnectionString:
                    deviceClient = await _azuredeviceClientCache.GetOrAddAsync(terminalId, (ICacheEntry x) => AzureIoTHubDeviceConnectionStringConnectAsync(terminalId, context));
                    break;
                case Models.AzureIotHubConnectionType.DeviceProvisioningService:
                    deviceClient = await _azuredeviceClientCache.GetOrAddAsync(terminalId, (ICacheEntry x) => AzureIoTHubDeviceProvisioningServiceConnectAsync(terminalId, context));
                    break;
                default:
                    _logger.LogError("Uplink- Azure IoT Hub ConnectionType unknown {0}", _azureIoTSettings.AzureIoTHub.ConnectionType);

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
                        _logger.LogWarning("Uplink-DeviceID:{0} RegisterAsync status:{1} failed ", terminalId, result.Status);

                        throw new ApplicationException($"Uplink-DeviceID:{0} RegisterAsync status:{1} failed");
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
