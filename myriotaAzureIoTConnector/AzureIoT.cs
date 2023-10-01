// Copyright (c) October 2023, devMobile Software
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
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Exceptions;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.PlugAndPlay;
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
    public partial class MyriotaUplinkMessageProcessor
    {
        private async Task<DeviceClient> DeviceConnectionStringConnectAsync(string terminalId, string application)
        {
            DeviceClient deviceClient;

            if (_azureIoTSettings.ApplicationToDtdlModelIdMapping.TryGetValue(application, out string? modelId))
            {
                ClientOptions clientOptions = new ClientOptions()
                {
                    ModelId = modelId
                };

                deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.AzureIoTHub.ConnectionString, terminalId, Constants.TransportSettings, clientOptions);
            }
            else
            {
                deviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.AzureIoTHub.ConnectionString, terminalId, Constants.TransportSettings);
            }

            return deviceClient;
        }

        private async Task<DeviceClient> DeviceProvisioningServiceConnectAsync(string terminalId, string application)
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

                    if (_azureIoTSettings.ApplicationToDtdlModelIdMapping.TryGetValue(application, out string? modelId))
                    {
                        ClientOptions clientOptions = new ClientOptions()
                        {
                            ModelId = modelId
                        };

                        ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new ProvisioningRegistrationAdditionalData()
                        {
                            JsonData = PnpConvention.CreateDpsPayload(modelId)
                        };
                        result = await provClient.RegisterAsync(provisioningRegistrationAdditionalData);
                    }
                    else
                    {
                        result = await provClient.RegisterAsync();
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

        private async Task<MethodResponse> DefaultMethodHandler(MethodRequest methodRequest, object userContext)
        {
            _logger.LogWarning("Downlink-TerminalId:{deviceId} DefaultMethodHandler name:{Name} payload:{DataAsJson}", (string)userContext, methodRequest.Name, methodRequest.DataAsJson);

            return new MethodResponse(Encoding.ASCII.GetBytes("{\"message\":\"The Myriota Connector does not support Direct Methods.\"}"), (int)HttpStatusCode.BadRequest);
        }
    }
}