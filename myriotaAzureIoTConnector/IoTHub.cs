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
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    internal partial class DeviceConnectionCache : IDeviceConnectionCache
    {
        public async Task AzureIoTHubMessageHandler(Message message, object context)
        {
            Models.DeviceConnectionContext deviceConnectionContext = (Models.DeviceConnectionContext)context;

            _logger.LogInformation("Downlink-IoT Hub TerminalId:{termimalId} LockToken:{LockToken}", deviceConnectionContext.TerminalId, message.LockToken);

            try
            {
                using (message)
                {
                    IFormatterDownlink payloadFormatterDownlink;

                    try
                    {
                        payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync(deviceConnectionContext.PayloadFormatterDownlink);
                    }
                    catch (CSScriptLib.CompilerException cex)
                    {
                        _logger.LogWarning(cex, "Downlink-terminalID:{terminalId} LockToken:{LockToken} payload formatter compilation failed", deviceConnectionContext.TerminalId, message.LockToken);

                        await deviceConnectionContext.DeviceClient.RejectAsync(message);

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
                        _logger.LogInformation("Downlink-DeviceID:{DeviceId} LockToken:{LockToken} payload not valid Text", deviceConnectionContext.TerminalId, message.LockToken);
                    }
                    catch (JsonReaderException jex)
                    {
                        _logger.LogInformation("Downlink-DeviceID:{DeviceId} LockToken:{LockToken} payload not valid JSON", deviceConnectionContext.TerminalId, message.LockToken);
                    }

                    byte[] payloadData = payloadFormatterDownlink.Evaluate(message.Properties, deviceConnectionContext.TerminalId, payloadJson, payloadBytes);

                    if (payloadData is null)
                    {
                        _logger.LogWarning("Downlink-terminalID:{terminalId} LockToken:{LockToken} payload formatter returned null", deviceConnectionContext.TerminalId, message.LockToken);

                        await deviceConnectionContext.DeviceClient.RejectAsync(message);

                        return;
                    }

                    if ((payloadData.Length < Constants.DownlinkPayloadMinimumLength) || (payloadData.Length > Constants.DownlinkPayloadMaximumLength))
                    {
                        _logger.LogWarning("Downlink-terminalID:{terminalId} LockToken:{LockToken} payloadData length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", deviceConnectionContext.TerminalId, message.LockToken, payloadData.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                        await deviceConnectionContext.DeviceClient.RejectAsync(message);

                        return;
                    }

                    _logger.LogInformation("Downlink-terminalID:{terminalId} LockToken:{LockToken} MessageID:{messageId} PayloadData:{payloadData} Length:{Length} sending", deviceConnectionContext.TerminalId, message.LockToken, message.MessageId, Convert.ToHexString(payloadData), payloadData.Length);

                    string messageId = await _myriotaModuleAPI.SendAsync(deviceConnectionContext.TerminalId, payloadData);

                    _logger.LogInformation("Downlink-terminalID:{terminalId} LockToken:{LockToken} MessageID:{messageId} sent", deviceConnectionContext.TerminalId, message.LockToken, messageId);

                    await deviceConnectionContext.DeviceClient.CompleteAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Downlink-MessageHandler processing failed");

                throw;
            }
        }
    }
}