﻿// Copyright (c) October 2023, devMobile Software
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
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.PlugAndPlay;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    public partial class MyriotaUplinkMessageProcessor
    {
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

        public async Task AzureIoTHubMessageHandler(Message message, object context)
        {
            string terminalId = (string)context;

            _logger.LogInformation("Downlink-IoT Hub TerminalId:{termimalId} LockToken:{LockToken}", terminalId, message.LockToken);

            try
            {
                using (message)
                {
                    DeviceClient deviceClient = await _deviceConnectionCache.GetAsync(terminalId);

                    IFormatterDownlink payloadFormatterDownlink;

                    try
                    {
                        payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync();
                    }
                    catch (CSScriptLib.CompilerException cex)
                    {
                        _logger.LogWarning(cex, "Downlink-terminalID:{terminalId} LockToken:{LockToken} payload formatter compilation failed", terminalId, message.LockToken);

                        await deviceClient.RejectAsync(message);

                        return;
                    }

                    byte[] payloadBytes = message.GetBytes();

                    JObject? payloadJson = null;

                    try
                    {
                        payloadJson = JObject.Parse(Encoding.UTF8.GetString(payloadBytes));
                    }
                    catch (ArgumentException aex)
                    {
                        _logger.LogInformation("Downlink-DeviceID:{DeviceId} LockToken:{LockToken} payload not valid Text", terminalId, message.LockToken);
                    }
                    catch (JsonReaderException jex)
                    {
                        _logger.LogInformation("Downlink-DeviceID:{DeviceId} LockToken:{LockToken} payload not valid JSON", terminalId, message.LockToken);
                    }

                    byte[] payloadData = payloadFormatterDownlink.Evaluate(message.Properties, terminalId, payloadJson, payloadBytes);

                    if (payloadData is null)
                    {
                        _logger.LogWarning("Downlink-terminalID:{terminalId} LockToken:{LockToken} payload formatter returned null", terminalId, message.LockToken);

                        await deviceClient.RejectAsync(message);

                        return;
                    }

                    if ((payloadData.Length < Constants.DownlinkPayloadMinimumLength) || (payloadData.Length > Constants.DownlinkPayloadMaximumLength))
                    {
                        _logger.LogWarning("Downlink-terminalID:{terminalId} LockToken:{LockToken} payloadData length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", terminalId, message.LockToken, payloadData.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                        await deviceClient.RejectAsync(message);

                        return;
                    }

                    _logger.LogInformation("Downlink-terminalID:{terminalId} LockToken:{LockToken} payloadData {payloadData} length:{Length} sent", terminalId, message.LockToken, Convert.ToHexString(payloadData), payloadData.Length);

                    await deviceClient.CompleteAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Downlink-MessageHandler processing failed");

                throw;
            }
        }

        private async Task<MethodResponse> DefaultMethodHandler(MethodRequest methodRequest, object userContext)
        {
            _logger.LogWarning("Downlink-TerminalId:{deviceId} DefaultMethodHandler name:{Name} payload:{DataAsJson}", (string)userContext, methodRequest.Name, methodRequest.DataAsJson);

            return new MethodResponse(Encoding.ASCII.GetBytes("{\"message\":\"The Myriota Connector does not support Direct Methods.\"}"), (int)HttpStatusCode.BadRequest);
        }
    }
}