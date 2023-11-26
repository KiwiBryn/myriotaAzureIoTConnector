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
   internal class IoTCentralDownlink(ILogger<IoTCentralDownlink> _logger, IOptions<Models.AzureIoT> azureIoTSettings, IPayloadFormatterCache _payloadFormatterCache, IMyriotaModuleAPI _myriotaModuleAPI) : IIoTCentralDownlink
   {
      private readonly Models.AzureIoT _azureIoTSettings = azureIoTSettings.Value;

      public async Task AzureIoTCentralMessageHandler(Message message, object userContext)
      {
         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

         _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken}", context.TerminalId, message.LockToken);

         try
         {
            // Check that Message has property, method-name so it can be processed correctly
            if (!message.Properties.TryGetValue("method-name", out string? methodName) || string.IsNullOrWhiteSpace(methodName))
            {
               _logger.LogWarning("Downlink- IoT Central TerminalId:{TerminalId} LockToken:{lockToken} method-name property missing or empty", context.TerminalId, message.LockToken);

               await context.DeviceClient.RejectAsync(message);

               return;
            }

            _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} method-name:{methodName}", context.TerminalId, message.LockToken, methodName);


            // Look up the method settings to get the option payload formatter and downlink message payload JSON.
            if ((_azureIoTSettings.IoTCentral.Methods == null) || !_azureIoTSettings.IoTCentral.Methods.TryGetValue(methodName, out Models.AzureIoTCentralMethod? method))
            {
               _logger.LogWarning("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} method-name:{methodName} has no settings", context.TerminalId, message.LockToken, methodName);

               await context.DeviceClient.RejectAsync(message);

               return;
            }

            // Use default formatter unless method formatter configured.
            string payloadFormatterName;
            if (string.IsNullOrEmpty(method.Formatter))
            {
               _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Context Formatter:{Formatter}", context.TerminalId, message.LockToken, context.PayloadFormatterDownlink);

               payloadFormatterName = context.PayloadFormatterDownlink;
            }
            else
            {
               _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Method Formatter:{Formatter}", context.TerminalId, message.LockToken, method.Formatter);

               payloadFormatterName = method.Formatter;
            }


            // Get the message as bytes 
            byte[] messageBytes;

            using (message)
            {
               messageBytes = message.GetBytes();
            }

            _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Message:{messageBytes}", context.TerminalId, message.LockToken, BitConverter.ToString(messageBytes));


            // Try converting the bytes to text then to JSON
            JObject? messageJson = null;

            try
            {
               string messageText = Encoding.UTF8.GetString(messageBytes).Trim();

               // special case for for "empty" payload
               if (messageText == "@")
               {
                  // If the method payload in the application configuration is broken nothing can be done
                  try
                  {
                     messageJson = JObject.Parse(method.Payload);
                  }
                  catch (JsonReaderException jex)
                  {
                     _logger.LogError(jex, "Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} method-name:{methodName} invalid Method.Payload:{method.Payload} ", context.TerminalId, message.LockToken, methodName, method.Payload);

                     await context.DeviceClient.RejectAsync(message);

                     return;
                  }
               }
               else
               {
                  // See if the message payload is valid JSON e.g. an object, vector etc.
                  try
                  {
                     messageJson = JObject.Parse(messageText);
                  }
                  catch (JsonReaderException)
                  {
                     // See if the message text is a valid property value e.g. enumeration, number, boolean etc.
                     try
                     {
                        messageJson = new JObject(new JProperty(methodName, JProperty.Parse(messageText)));
                     }
                     catch (JsonException)
                     {
                        // if not it must be a property e.g. a string value WARNING - That doesn't look like valid JSON
                        messageJson = new JObject(new JProperty(methodName, messageText));
                     }
                  }
               }
            }
            // When Encoding.UTF8.GetString is broken
            catch (ArgumentException aex)
            {
               _logger.LogError(aex, "Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} is not valid text", context.TerminalId, message.LockToken);
            }

            if (messageJson is null)
            {
               _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken}", context.TerminalId, message.LockToken, JsonConvert.SerializeObject(messageJson, Formatting.Indented));
            }
            else
            {
               _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Message JSON:{messageJson}", context.TerminalId, message.LockToken, JsonConvert.SerializeObject(messageJson, Formatting.Indented));
            }



            // This shouldn't fail, but it could for lots of diffent reasons, invalid path to blob, syntax error, interface broken etc.
            IFormatterDownlink payloadFormatter = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatterName);

            // This will fail when with null references, divide by zero, out of range etc. when paylaod formatter code has issues
            byte[] payloadBytes = payloadFormatter.Evaluate(message.Properties, context.TerminalId, messageJson, messageBytes);


            // Validate payload before calling Myriota control message send API method
            if (payloadBytes is null)
            {
               _logger.LogWarning("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Evaluate returned null", context.TerminalId, message.LockToken);

               await context.DeviceClient.RejectAsync(message);

               return;
            }

            if ((payloadBytes.Length < Constants.DownlinkPayloadMinimumLength) || (payloadBytes.Length > Constants.DownlinkPayloadMaximumLength))
            {
               _logger.LogWarning("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Payload:{payloadBytes} length:{length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, message.LockToken, BitConverter.ToString(payloadBytes), payloadBytes.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

               await context.DeviceClient.RejectAsync(message);

               return;
            }

            _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} Payload:{payloadBytes} Length:{length}", context.TerminalId, message.LockToken, BitConverter.ToString(payloadBytes), payloadBytes.Length);

            // Finally send the message using Myriota API
            _logger.LogInformation("Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} sending", context.TerminalId, message.LockToken);

            string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

            _logger.LogInformation("Downlink- IoT Central TerminalID:{terminalId} LockToken:{lockToken} MessageID:{messageId} sent", context.TerminalId, message.LockToken, messageId);

            await context.DeviceClient.CompleteAsync(message);
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Downlink- IoT Central TerminalID:{TerminalId} LockToken:{lockToken} MessageHandler processing failed", context.TerminalId, message.LockToken);

            await context.DeviceClient.RejectAsync(message);
         }
      }
   }
}