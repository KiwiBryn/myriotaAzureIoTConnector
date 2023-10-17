﻿// Copyright (c) September 2023, devMobile Software
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
    internal partial class DeviceConnectionCache : IDeviceConnectionCache
    {
        private readonly ILogger _logger;
        private readonly Models.AzureIoT _azureIoTSettings;
        private readonly IPayloadFormatterCache _payloadFormatterCache;
        private readonly IMyriotaModuleAPI _myriotaModuleAPI;

        private static readonly IAppCache _deviceConnectionCache = new CachingService();

        public DeviceConnectionCache(ILoggerFactory loggerFactory, IOptions<Models.AzureIoT> azureIoTSettings, IPayloadFormatterCache payloadFormatterCache, IMyriotaModuleAPI myriotaModuleAPI)
        {
            _logger = loggerFactory.CreateLogger<MyriotaUplinkMessageProcessor>();
            _azureIoTSettings = azureIoTSettings.Value;
            _payloadFormatterCache = payloadFormatterCache;
            _myriotaModuleAPI = myriotaModuleAPI;
        }

        public async Task<Models.DeviceConnectionContext> GetOrAddAsync(string terminalId, CancellationToken cancellationToken)
        {
            Models.DeviceConnectionContext context;

            switch (_azureIoTSettings.AzureIoTHub.ConnectionType)
            {
                case Models.AzureIotHubConnectionType.DeviceConnectionString:
                    context = await _deviceConnectionCache.GetOrAddAsync(terminalId, (ICacheEntry x) => DeviceConnectionStringConnectAsync(terminalId, cancellationToken), memoryCacheEntryOptions);
                    break;
                case Models.AzureIotHubConnectionType.DeviceProvisioningService:
                    context = await _deviceConnectionCache.GetOrAddAsync(terminalId, (ICacheEntry x) => DeviceProvisioningServiceConnectAsync(terminalId, cancellationToken), memoryCacheEntryOptions);
                    break;
                default:
                    _logger.LogError("Uplink- Azure IoT Hub ConnectionType unknown {0}", _azureIoTSettings.AzureIoTHub.ConnectionType);

                    throw new NotImplementedException("AzureIoT Hub unsupported ConnectionType");
            }

            await context.DeviceClient.SetMethodDefaultHandlerAsync(DefaultMethodHandler, context, cancellationToken);

            await context.DeviceClient.SetReceiveMessageHandlerAsync(AzureIoTHubMessageHandler, context, cancellationToken);

            await context.DeviceClient.OpenAsync(cancellationToken);

            return context;
        }

        private async Task<Models.DeviceConnectionContext> DeviceConnectionStringConnectAsync(string terminalId, CancellationToken cancellationToken)
        {
            Models.Item item = await _myriotaModuleAPI.GetAsync(terminalId, cancellationToken);

            if (!item.Attributes.TryGetValue("DeviceType", out string moduleType))
            {
                moduleType = _azureIoTSettings.ModuleType;
            }

            if (!item.Attributes.TryGetValue("DtdlModelId", out string dtdlModelId))
            {
                dtdlModelId = _azureIoTSettings.DtdlModelId;
            }

            ClientOptions clientOptions = null;

            if (!string.IsNullOrEmpty(dtdlModelId))
            {
                clientOptions = new ClientOptions()
                {
                    ModelId = dtdlModelId
                };
            }

            return new Models.DeviceConnectionContext()
            {
                TerminalId = terminalId,
                ModuleType = moduleType,
                DeviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.AzureIoTHub.ConnectionString, terminalId, Constants.TransportSettings, clientOptions),
                Attibutes = new Dictionary<string, string>(item.Attributes)
            };
        }

        private async Task<Models.DeviceConnectionContext> DeviceProvisioningServiceConnectAsync(string terminalId, CancellationToken cancellationToken)
        {
            Models.Item item = await _myriotaModuleAPI.GetAsync(terminalId, cancellationToken);

            if (!item.Attributes.TryGetValue("DeviceType", out string moduleType))
            {
                moduleType = _azureIoTSettings.ModuleType;
            }

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

                    if (!item.Attributes.TryGetValue("DtdlModelId", out string dtdlModelId))
                    {
                        dtdlModelId = _azureIoTSettings.DtdlModelId;
                    }

                    if (!string.IsNullOrEmpty(dtdlModelId))
                    {
                        ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new ProvisioningRegistrationAdditionalData()
                        {
                            JsonData = PnpConvention.CreateDpsPayload(dtdlModelId)
                        };

                        result = await provClient.RegisterAsync(provisioningRegistrationAdditionalData, cancellationToken);
                    }
                    else
                    {
                        result = await provClient.RegisterAsync(cancellationToken);
                    }

                    if (result.Status != ProvisioningRegistrationStatusType.Assigned)
                    {
                        _logger.LogWarning("Uplink-DeviceID:{0} RegisterAsync status:{1} failed ", terminalId, result.Status);

                        throw new ApplicationException($"Uplink-DeviceID:{0} RegisterAsync status:{1} failed");
                    }

                    IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

                    return new Models.DeviceConnectionContext()
                    {
                        TerminalId = terminalId,
                        ModuleType = moduleType,
                        DeviceClient = DeviceClient.Create(result.AssignedHub, authentication, Constants.TransportSettings),
                        Attibutes = new Dictionary<string, string>(item.Attributes)
                    };
                }
            }
        }

        public async Task TerminalListLoad(CancellationToken cancellationToken)
        {
            foreach (Models.Item item in await _myriotaModuleAPI.ListAsync(cancellationToken))
            {
                _logger.LogInformation("Myriota TerminalId:{TerminalId}", item.Id);

                await this.GetOrAddAsync(item.Id, cancellationToken);
            }
        }

        private async Task<MethodResponse> DefaultMethodHandler(MethodRequest methodRequest, object userContext)
        {
            Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

            _logger.LogWarning("Downlink-TerminalId:{deviceId} DefaultMethodHandler name:{Name} payload:{DataAsJson}", context.TerminalId, methodRequest.Name, methodRequest.DataAsJson);

            return new MethodResponse(Encoding.ASCII.GetBytes("{\"message\":\"The Myriota Connector does not support Direct Methods.\"}"), (int)HttpStatusCode.BadRequest);
        }

        private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        {
            Priority = CacheItemPriority.NeverRemove
        };
    }
}
