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
      public async Task AzureIoTCentralMessageHandler(Message message, object userContext)
      {
         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;  

         _logger.LogInformation("Downlink-IoT Central TerminalID:{TerminalId} LockToken:{LockToken}", context.TerminalId, message.LockToken);

         try
         {
            using (message)
            {
               // Check that Message has property, method-name so it can be processed correctly
               if (!message.Properties.TryGetValue("method-name", out string methodName) || string.IsNullOrWhiteSpace(methodName))
               {
                  _logger.LogWarning("Downlink-TerminalId:{DeviceId} LockToken:{LockToken} method-name:property missing or empty", context.TerminalId, message.LockToken);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               // Look up the method settings to get UserApplicationId and optional downlink message payload JSON.
               if ((_azureIoTSettings.IoTCentral.Methods == null) || !_azureIoTSettings.IoTCentral.Methods.TryGetValue(methodName, out Models.AzureIoTCentralMethod method))
               {
                  _logger.LogWarning("Downlink-TerminalID:{TerminalId} LockToken:{LockToken} method-name:{methodName} has no payload", context.TerminalId, message.LockToken, methodName);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               // Use default formatter and replace with method formatter if configured.
               string payloadFormatter = context.PayloadFormatterDownlink;

               if (!string.IsNullOrEmpty(method.PayloadFormatter))
               {
                  payloadFormatter = context.PayloadFormatterDownlink;
               }

               // Get the message payload try converting it to text then to JSON
               byte[] payloadBytes = message.GetBytes();

               string payloadText = string.Empty;

               try
               {
                  payloadText = Encoding.UTF8.GetString(payloadBytes);
               }
               catch (FormatException fex)
               {
                  _logger.LogWarning(fex, "Downlink-DeviceId:{DeviceId} LockToken:{LockToken} Encoding.UTF8.GetString(2) failed", context.TerminalId, message.LockToken, BitConverter.ToString(payloadBytes));
               }

               JObject payloadJson = null;

               // Check to see if special case for Azure IoT central command with no request payload
               if (payloadText.IsPayloadEmpty())
               {
                  if (method.Payload.IsPayloadValidJson())
                  {
                     payloadJson = JObject.Parse(method.Payload);
                  }
                  else
                  {
                     _logger.LogWarning("Downlink-DeviceID:{DeviceId} LockToken:{LockToken} method-name:{methodName} IsPayloadValidJson:{Payload} failed", context.TerminalId, message.LockToken, methodName, method.Payload);

                     await context.DeviceClient.RejectAsync(message);

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

               _logger.LogInformation("Downlink-TeminalID:{TerminalId} LockToken:{LockToken} Method:{methodName} Payload:{3}", context.DeviceClient, message.LockToken, methodName, BitConverter.ToString(payloadBytes));

               IFormatterDownlink payloadFormatterDownlink;

               try
               {
                  payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatter);
               }
               catch (CSScriptLib.CompilerException cex)
               {
                  _logger.LogWarning(cex, "Downlink-TerminalID:{terminalId} LockToken:{LockToken} payload formatter compilation failed", context.TerminalId, message.LockToken);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               byte[] payloadData = payloadFormatterDownlink.Evaluate(message.Properties, context.TerminalId, payloadJson, payloadBytes);

               if (payloadData is null)
               {
                  _logger.LogWarning("Downlink-terminalID:{TerminalId} LockToken:{LockToken} payload formatter returned null", context.TerminalId, message.LockToken);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               if ((payloadData.Length < Constants.DownlinkPayloadMinimumLength) || (payloadData.Length > Constants.DownlinkPayloadMaximumLength))
               {
                  _logger.LogWarning("Downlink-TerminalID:{terminalId} LockToken:{LockToken} payloadData length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, message.LockToken, payloadData.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               _logger.LogInformation("Downlink-terminalID:{terminalId} LockToken:{LockToken} PayloadData:{payloadData} Length:{Length} sending", context.TerminalId, message.LockToken, Convert.ToHexString(payloadData), payloadData.Length);

               string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadData);

               _logger.LogInformation("Downlink-terminalID:{terminalId} LockToken:{LockToken} MessageID:{messageId} sent", context.TerminalId, message.LockToken, messageId);

               await context.DeviceClient.CompleteAsync(message);
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Downlink-TerminalID:{DeviceId} LockToken:{TerminalId} MessageHandler processing failed", context.TerminalId, message.LockToken);

            await context.DeviceClient.RejectAsync(message);
         }
      }
   }
}