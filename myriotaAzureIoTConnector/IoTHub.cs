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
            string terminalId = (string)context;

            _logger.LogInformation("Downlink-IoT Hub TerminalId:{termimalId} LockToken:{LockToken}", terminalId, message.LockToken);

            try
            {
                using (message)
                {
                    DeviceClient deviceClient = await this.GetAsync(terminalId);

                    IFormatterDownlink payloadFormatterDownlink;

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
                        _logger.LogInformation("Downlink-TerminalID:{terminalId} LockToken:{LockToken} payload not valid JSON", terminalId, message.LockToken);
                    }

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