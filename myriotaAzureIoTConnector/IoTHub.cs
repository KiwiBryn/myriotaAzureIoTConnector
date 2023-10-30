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
      public async Task AzureIoTHubMessageHandler(Message message, object userContext)
      {
         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

         _logger.LogInformation("Downlink-IoT Hub TerminalID:{terminalId} LockToken:{LockToken}", context.TerminalId, message.LockToken);

         try
         {
            using (message)
            {
               // Use default formatter and replace with message property formatter if configured.
               string payloadFormatter = context.PayloadFormatterDownlink;

               if (message.Properties.TryGetValue("PayloadFormatter", out string formatter) && (!string.IsNullOrEmpty(formatter)))
               {
                  payloadFormatter = formatter;
               }

               IFormatterDownlink payloadFormatterDownlink;

               try
               {
                  payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatter);
               }
               catch (CSScriptLib.CompilerException cex)
               {
                  _logger.LogWarning(cex, "Downlink-TerminalID:{TerminalId} LockToken:{LockToken} payload formatter:{payloadFormatter} compilation failed", context.TerminalId, message.LockToken, payloadFormatter);

                  await context.DeviceClient.RejectAsync(message);

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
                  _logger.LogInformation("Downlink-TerminalID:{TerminalId} LockToken:{LockToken} payload not valid Text", context.TerminalId, message.LockToken);
               }
               catch (JsonReaderException jex)
               {
                  _logger.LogInformation("Downlink-TerminalID:{TerminalId} LockToken:{LockToken} payload not valid JSON", context.TerminalId, message.LockToken);
               }

               byte[] payloadData = payloadFormatterDownlink.Evaluate(message.Properties, context.TerminalId, payloadJson, payloadBytes);

               if (payloadData is null)
               {
                  _logger.LogWarning("Downlink-TerminalID:{TerminalId} LockToken:{LockToken} payload formatter returned null", context.TerminalId, message.LockToken);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               if ((payloadData.Length < Constants.DownlinkPayloadMinimumLength) || (payloadData.Length > Constants.DownlinkPayloadMaximumLength))
               {
                  _logger.LogWarning("Downlink-TerminalID:{TerminalId} LockToken:{LockToken} payloadData length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, message.LockToken, payloadData.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               _logger.LogInformation("Downlink-TerminalID:{TerminalId} LockToken:{LockToken} PayloadData:{payloadData} Length:{Length} sending", context.TerminalId, message.LockToken, Convert.ToHexString(payloadData), payloadData.Length);

               string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadData);

               _logger.LogInformation("Downlink-TerminalID:{TerminalId} LockToken:{LockToken} MessageID:{messageId} sent", context.TerminalId, message.LockToken, messageId);

               await context.DeviceClient.CompleteAsync(message);
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Downlink-TerminalID:{TerminalID} LockToken:{LockToken} MessageHandler processing failed", context.TerminalId, message.LockToken);

            await context.DeviceClient.RejectAsync(message);
         }
      }
   }
}