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
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{ 
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using PayloadFormatter;


    internal partial class DeviceConnectionCache : IDeviceConnectionCache
    {
        public async Task AzureIoTCentralMessageHandler(Message message, object context)
        {
            string terminalId = (string)context;

            try
            {
                _logger.LogInformation("Downlink-IoT Central TerminalID:{termimalId} LockToken:{LockToken}", terminalId, message.LockToken);

                using (message)
                {
                    DeviceClient deviceClient = await this.GetAsync(terminalId);

                    IFormatterDownlink payloadFormatterDownlink;

                    // Check that Message has property, method-name so it can be processed correctly
                    if (!message.Properties.TryGetValue("method-name", out string methodName) || string.IsNullOrWhiteSpace(methodName))
                    {
                        _logger.LogWarning("Downlink-TerminalID:{DeviceId} LockToken:{LockToken} method-name:property missing or empty", terminalId, message.LockToken);

                        await deviceClient.RejectAsync(message);

                        return;
                    }

                    /*
                    // Look up the method settings to get UserApplicationId and optional downlink message payload JSON.
                    if ((_azureIoTSettings.AzureIoTCentral.Methods == null) || !_azureIoTSettings.AzureIoTCentral.Methods.TryGetValue(methodName, out Models.AzureIoTCentralMethodSetting methodSetting))
                    {
                        _logger.LogWarning("Downlink-TerminalID:{terminalId} LockToken:{LockToken} method-name:{methodName} has no settings", terminalId, message.LockToken, methodName);

                        await deviceClient.RejectAsync(message);

                        return;
                    }
                    */

                    try
                    {
                        payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync();
                    }
                    catch (CSScriptLib.CompilerException cex)
                    {
                        _logger.LogWarning(cex, "Downlink-TerminalID:{terminalId} LockToken:{LockToken} payload formatter compilation failed", terminalId, message.LockToken);

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
                        _logger.LogInformation("Downlink-TerminalID:{terminalId} LockToken:{LockToken} payload not valid Text", terminalId, message.LockToken);
                    }
                    catch (JsonReaderException jex)
                    {
                        _logger.LogInformation("Downlink-TerminalId:{terminalId} LockToken:{LockToken} payload not valid JSON", terminalId, message.LockToken);
                    }

                    /*
                    // Check to see if special case for Azure IoT central command with no request payload
                    if (payloadText.IsPayloadEmpty())
                    {
                        if (methodSetting.Payload.IsPayloadValidJson())
                        {
                            payloadJson = JObject.Parse(methodSetting.Payload);
                        }
                        else
                        {
                            _logger.LogWarning("Downlink-TerminalId:{terminalId} LockToken:{LockToken} method-name:{methodName} IsPayloadValidJson:{Payload} failed", terminalId, message.LockToken, methodName, methodSetting.Payload);

                            await deviceClient.RejectAsync(message);

                            return;
                        }
                    }
                    else
                    {
                        if (payloadText.IsPayloadValidJson())
                        {
                            payloadJson = JObject.Parse(payloadText);
                        }
                        else
                        {
                            // Normally wouldn't use exceptions for flow control but, I can't think of a better way...
                            try
                            {
                                payloadJson = new JObject(new JProperty(methodName, JProperty.Parse(payloadText)));
                            }
                            catch (JsonException ex)
                            {
                                payloadJson = new JObject(new JProperty(methodName, payloadText));
                            }
                        }
                    }
                    */

                    byte[] payloadData = payloadFormatterDownlink.Evaluate(message.Properties, terminalId, payloadJson, payloadBytes);

                    if (payloadData is null)
                    {
                        _logger.LogWarning("Downlink-TerminalID:{terminalId} LockToken:{LockToken} payload formatter returned null", terminalId, message.LockToken);

                        await deviceClient.RejectAsync(message);

                        return;
                    }

                    if ((payloadData.Length < Constants.DownlinkPayloadMinimumLength) || (payloadData.Length > Constants.DownlinkPayloadMaximumLength))
                    {
                        _logger.LogWarning("Downlink-TerminalID:{terminalId} LockToken:{LockToken} payloadData length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", terminalId, message.LockToken, payloadData.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                        await deviceClient.RejectAsync(message);

                        return;
                    }

                    await _myriotaModuleAPI.SendAsync(terminalId, payloadData);

                    _logger.LogInformation("Downlink-TerminalID:{terminalId} LockToken:{LockToken} payloadData {payloadData} length:{Length} sent", terminalId, message.LockToken, Convert.ToHexString(payloadData), payloadData.Length);

                    await deviceClient.CompleteAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Downlink-TerminalID:{terminalId} LockToken:{LockToken} MessageHandler processing failed", terminalId, message.LockToken);

                throw;
            }
        }
    }
}
