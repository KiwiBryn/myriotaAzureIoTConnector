// Copyright (c) September 2023, devMobile Software
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
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client.PlugAndPlay;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Shared;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using LazyCache;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    public interface IDeviceConnectionCache
    {
        public Task<DeviceClient> GetOrAddAsync(string terminalId, CancellationToken cancellationToken);

        public Task<DeviceClient> GetAsync(string terminalId);
    }

    internal partial class DeviceConnectionCache : IDeviceConnectionCache
    {
        private readonly ILogger _logger;
        private readonly Models.AzureIoT _azureIoTSettings;
        private readonly IPayloadFormatterCache _payloadFormatterCache;

        private static readonly IAppCache _deviceConnectionCache = new CachingService();

        public DeviceConnectionCache(ILoggerFactory loggerFactory, IOptions<Models.AzureIoT> azureIoTSettings, IPayloadFormatterCache payloadFormatterCache)
        {
            _logger = loggerFactory.CreateLogger<MyriotaUplinkMessageProcessor>();
            _azureIoTSettings = azureIoTSettings.Value;
            _payloadFormatterCache = payloadFormatterCache;
        }

        public async Task<DeviceClient> GetOrAddAsync(string terminalId, CancellationToken cancellationToken)
        {
            DeviceClient deviceClient;

            switch (_azureIoTSettings.AzureIoTHub.ConnectionType)
            {
                case Models.AzureIotHubConnectionType.DeviceConnectionString:
                    deviceClient = await _deviceConnectionCache.GetOrAddAsync(terminalId, (ICacheEntry x) => DeviceConnectionStringConnectAsync(terminalId), memoryCacheEntryOptions);
                    break;
                case Models.AzureIotHubConnectionType.DeviceProvisioningService:
                    deviceClient = await _deviceConnectionCache.GetOrAddAsync(terminalId, (ICacheEntry x) => DeviceProvisioningServiceConnectAsync(terminalId, cancellationToken), memoryCacheEntryOptions);
                    break;
                default:
                    _logger.LogError("Uplink- Azure IoT Hub ConnectionType unknown {0}", _azureIoTSettings.AzureIoTHub.ConnectionType);

                    throw new NotImplementedException("AzureIoT Hub unsupported ConnectionType");
            }

            await deviceClient.SetMethodDefaultHandlerAsync(DefaultMethodHandler, terminalId, cancellationToken);

            await deviceClient.OpenAsync(cancellationToken);

            return deviceClient;
        }

        private async Task<DeviceClient> DeviceConnectionStringConnectAsync(string terminalId)
        {
            DeviceClient deviceClient;

            if (string.IsNullOrEmpty(_azureIoTSettings.DtdlModelId))
            {
                deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.AzureIoTHub.ConnectionString, terminalId, Constants.TransportSettings);
            }
            else
            {
                ClientOptions clientOptions = new ClientOptions()
                {
                    ModelId = _azureIoTSettings.DtdlModelId
                };

                deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.AzureIoTHub.ConnectionString, terminalId, Constants.TransportSettings, clientOptions);

            }

            return deviceClient;
        }

        private async Task<DeviceClient> DeviceProvisioningServiceConnectAsync(string terminalId, CancellationToken cancellationToken)
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


                    if (string.IsNullOrEmpty(_azureIoTSettings.DtdlModelId))
                    {
                        result = await provClient.RegisterAsync(cancellationToken);
                    }
                    else
                    {
                        ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new ProvisioningRegistrationAdditionalData()
                        {
                            JsonData = PnpConvention.CreateDpsPayload(_azureIoTSettings.DtdlModelId)
                        };

                        result = await provClient.RegisterAsync(provisioningRegistrationAdditionalData, cancellationToken);
                    }

                    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                    {
                        _logger.LogWarning("Uplink-DeviceID:{0} RegisterAsync status:{1} failed ", terminalId, result.Status);

                        throw new ApplicationException($"Uplink-DeviceID:{0} RegisterAsync status:{1} failed");
                    }

                    IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                    deviceClient = DeviceClient.Create(result.AssignedHub, authentication, Constants.TransportSettings);
                }
            }

            return deviceClient;
        }

        public async Task<DeviceClient> GetAsync(string terminalId)
        {
            return await _deviceConnectionCache.GetAsync<DeviceClient>(terminalId);
        }

        private async Task<MethodResponse> DefaultMethodHandler(MethodRequest methodRequest, object userContext)
        {
            _logger.LogWarning("Downlink-TerminalId:{deviceId} DefaultMethodHandler name:{Name} payload:{DataAsJson}", (string)userContext, methodRequest.Name, methodRequest.DataAsJson);

            return new MethodResponse(Encoding.ASCII.GetBytes("{\"message\":\"The Myriota Connector does not support Direct Methods.\"}"), (int)HttpStatusCode.BadRequest);
        }

        private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        {
            Priority = CacheItemPriority.NeverRemove
        };
    }
}
