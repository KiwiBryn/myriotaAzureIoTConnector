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
using Microsoft.Extensions.Options;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
   internal class IoTCentralDownlink : IIoTCentralDownlink
   {
      private readonly ILogger<IoTCentralDownlink> _logger;
      private readonly Models.AzureIoT _azureIoTSettings;
      private readonly IPayloadFormatterCache _payloadFormatterCache;
      private readonly IMyriotaModuleAPI _myriotaModuleAPI;

      public IoTCentralDownlink(ILogger<IoTCentralDownlink> logger, IOptions<Models.AzureIoT> azureIoTSettings, IPayloadFormatterCache payloadFormatterCache, IMyriotaModuleAPI myriotaModuleAPI)
      {
         _logger = logger;
         _azureIoTSettings = azureIoTSettings.Value;
         _payloadFormatterCache = payloadFormatterCache;
         _myriotaModuleAPI = myriotaModuleAPI;
      }

      public async Task AzureIoTCentralMessageHandler(Message message, object userContext)
      {
         string lockToken = message.LockToken;
         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;  

         _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken}", context.TerminalId, lockToken);

         try
         {
            using (message)
            {
               // Check that Message has property, method-name so it can be processed correctly
               if (!message.Properties.TryGetValue("method-name", out string methodName) || string.IsNullOrWhiteSpace(methodName))
               {
                  _logger.LogWarning("Downlink- IoT Central TerminalId:{TerminalId} LockToken:{lockToken} method-name:property missing or empty", context.TerminalId, lockToken);

                  await context.DeviceClient.RejectAsync(lockToken);

                  return;
               }

               // Look up the method settings to get the option payload formatter and downlink message payload JSON.
               if ((_azureIoTSettings.IoTCentral.Methods == null) || !_azureIoTSettings.IoTCentral.Methods.TryGetValue(methodName, out Models.AzureIoTCentralMethod method))
               {
                  _logger.LogWarning("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} method-name:{methodName} has no settings", context.TerminalId, lockToken, methodName);

                  await context.DeviceClient.RejectAsync(lockToken);

                  return;
               }

               // Use default formatter and replace with method formatter if configured.
               string payloadFormatterName = context.PayloadFormatterDownlink;
               if (!string.IsNullOrEmpty(method.Formatter))
               {
                  payloadFormatterName = method.Formatter;
               }

               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TermimalId} LockToken:{lockToken} Payload formatter:{payloadFormatter} ", context.TerminalId, lockToken, payloadFormatterName);


               // Get the message payload try converting it to text then to JSON
               byte[] messageBytes = message.GetBytes();

               // This will fail for some messages, payload formatter gets bytes only
               string messageText = string.Empty;
               try
               {
                  messageText = Encoding.UTF8.GetString(messageBytes);
               }
               catch (FormatException fex)
               {
                  _logger.LogWarning(fex, "Downlink- IoT Central TerminalId:{TerminalId} LockToken:{lockToken} Encoding.UTF8.GetString(messageBytes) failed", context.TerminalId, lockToken, BitConverter.ToString(messageBytes));
               }

               JObject? messageJson = null;

               // Check to see if special case for Azure IoT central command with no request payload
               if (messageText.IsPayloadEmpty())
               {
                  if (method.Payload.IsPayloadValidJson())
                  {
                     messageJson = JObject.Parse(method.Payload);
                  }
                  else
                  {
                     _logger.LogWarning("Downlink- IoT Central TerminalId:{TerminalId} LockToken:{lockToken} method-name:{methodName} IsPayloadValidJson:{Payload} failed", context.TerminalId, lockToken, methodName, method.Payload);

                     await context.DeviceClient.RejectAsync(lockToken);

                     return;
                  }
               }
               else
               {
                  if (messageText.IsPayloadValidJson())
                  {
                     messageJson = JObject.Parse(messageText);
                  }
                  else
                  {
                     // Normally wouldn't use exceptions for flow control but, I can't think of a better way...
                     try
                     {
                        messageJson = new JObject(new JProperty(methodName, JProperty.Parse(messageText)));
                     }
                     catch (JsonException ex)
                     {
                        messageJson = new JObject(new JProperty(methodName, messageText));
                     }
                  }
               }

               _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Method:{methodName} Payload:{3}", context.TerminalId, lockToken, methodName, BitConverter.ToString(messageBytes));

               // This shouldn't fail, but it could for lots of diffent reasons, invalid path to blob, syntax error, interface broken etc.
               IFormatterDownlink payloadFormatter = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatterName);

               // This shouldn't fail, but it could for lots of different reasons, null references, divide by zero, out of range etc.
               byte[] payloadBytes = payloadFormatter.Evaluate(message.Properties, context.TerminalId, messageJson, messageBytes);

               // Validate payload before calling Myriota control message send API method
               if (payloadBytes is null)
               {
                  _logger.LogWarning("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} payload formatter:{payloadFormatter} Evaluate returned null", context.TerminalId, lockToken, payloadFormatterName);

                  await context.DeviceClient.RejectAsync(lockToken);

                  return;
               }

               if ((payloadBytes.Length < Constants.DownlinkPayloadMinimumLength) || (payloadBytes.Length > Constants.DownlinkPayloadMaximumLength))
               {
                  _logger.LogWarning("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} PayloadBytes:{payloadBytes} length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, lockToken, Convert.ToHexString(payloadBytes), payloadBytes.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                  await context.DeviceClient.RejectAsync(lockToken);

                  return;
               }

               // This shouldn't fail, but it could few reasons mainly connectivity & message queuing etc.
               _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} PayloadBytes:{payloadBytes} Length:{Length} sending", context.TerminalId, lockToken, Convert.ToHexString(payloadBytes), payloadBytes.Length);

               // Finally send the message using Myriota API
               string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

               _logger.LogInformation("Downlink- IoT Central TerminalID:{terminalId} LockToken:{lockToken} MessageID:{messageId} sent", context.TerminalId, lockToken, messageId);

               await context.DeviceClient.CompleteAsync(lockToken);
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} MessageHandler processing failed", context.TerminalId, lockToken);

            await context.DeviceClient.RejectAsync(lockToken);
         }
      }
   }
}