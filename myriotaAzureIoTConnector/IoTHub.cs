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
         string lockToken = message.LockToken;
         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

         _logger.LogInformation("Downlink- IoT Hub TerminalId:{termimalId} LockToken:{LockToken}", context.TerminalId, lockToken);

         // Use default formatter and replace with message specific formatter if configured.
         string payloadFormatterName;
         if (!message.Properties.TryGetValue(Constants.IoTHubDownlinkPayloadFormatterProperty, out payloadFormatterName) || string.IsNullOrEmpty(payloadFormatterName))
         {
            payloadFormatterName = context.PayloadFormatterDownlink;
         }

         _logger.LogInformation("Downlink- IoT Hub TerminalID:{termimalId} LockToken:{LockToken} Payload formatter:{payloadFormatter} ", context.TerminalId, lockToken, payloadFormatterName);

         try
         {
            // If this fails payload broken
            byte[] messageBytes = message.GetBytes();

            // This will fail for some messages, payload formatter gets bytes only
            string messageText = string.Empty;
            try
            {
               messageText = Encoding.UTF8.GetString(messageBytes);
            }
            catch (ArgumentException aex)
            {
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} messageBytes:{2} not valid Text", context.TerminalId, lockToken, BitConverter.ToString(messageBytes));
            }

            // This will fail for some messages, payload formatter gets bytes only
            JObject? messageJson = null;
            try
            {
               messageJson = JObject.Parse(messageText);
            }
            catch ( JsonReaderException jex)
            {
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} messageText:{2} not valid json", context.TerminalId, lockToken, BitConverter.ToString(messageBytes));
            }

            // This shouldn't fail, but it could for lots of diffent reasons, invalid path to blob, syntax error, interface broken etc.
            IFormatterDownlink payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatterName);

            // This shouldn't fail, but it could for lots of different reasons, null references, divide by zero, out of range etc.
            byte[] payloadBytes = payloadFormatterDownlink.Evaluate(message.Properties, context.TerminalId, messageJson, messageBytes);

            // Validate payload before calling Myriota control message send API method
            if (payloadBytes is null)
            {
               _logger.LogWarning("Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} payload formatter:{payloadFormatter} Evaluate returned null", context.TerminalId, lockToken, payloadFormatterName);

               await context.DeviceClient.RejectAsync(message);

               return;
            }

            if ((payloadBytes.Length < Constants.DownlinkPayloadMinimumLength) || (payloadBytes.Length > Constants.DownlinkPayloadMaximumLength))
            {
               _logger.LogWarning("Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} payloadData length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, lockToken, payloadBytes.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

               await context.DeviceClient.RejectAsync(message);

               return;
            }

            // This shouldn't fail, but it could few reasons mainly connectivity & message queuing etc.
            _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} PayloadData:{payloadData} Length:{Length} sending", context.TerminalId, lockToken, Convert.ToHexString(payloadBytes), payloadBytes.Length);

            // Finally send the message using Myriota API
            string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

            _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} MessageID:{messageId} sent", context.TerminalId, lockToken, messageId);

            await context.DeviceClient.CompleteAsync(message);
         }
         catch (Exception ex)
         {
            await context.DeviceClient.RejectAsync(message);

            _logger.LogError(ex, "Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} failed", context.TerminalId, lockToken);
         }
         finally
         {
            // Mop up the non managed resources of message
            message.Dispose();
         }
      }
   }
}

