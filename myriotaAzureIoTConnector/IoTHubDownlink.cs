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
   internal class IoTHubDownlink(ILogger<IoTHubDownlink> _logger, IPayloadFormatterCache _payloadFormatterCache, IMyriotaModuleAPI _myriotaModuleAPI) : IIoTHubDownlink
   {
      public async Task AzureIoTHubMessageHandler(Message message, object userContext)
      {
         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

         _logger.LogInformation("Downlink- IoT Hub TerminalId:{TermimalId} LockToken:{LockToken}", context.TerminalId, message.LockToken);

         using (message) // https://learn.microsoft.com/en-us/dotnet/api/microsoft.azure.devices.client.deviceclient.setreceivemessagehandlerasync?view=azure-dotnet
         {
            try
            {
               // Replace default formatter with message specific formatter if configured.
               if (!message.Properties.TryGetValue(Constants.IoTHubDownlinkPayloadFormatterProperty, out string? payloadFormatterName) || string.IsNullOrEmpty(payloadFormatterName))
               {
                  _logger.LogInformation("Downlink- IoT Hub TerminalID:{TermimalId} LockToken:{LockToken} Context formatter:{payloadFormatterName} ", context.TerminalId, message.LockToken, payloadFormatterName);

                  payloadFormatterName = context.PayloadFormatterDownlink;
               }
               else
               {
                  _logger.LogInformation("Downlink- IoT Hub TerminalID:{TermimalId} LockToken:{LockToken} Property formatter:{payloadFormatterName} ", context.TerminalId, message.LockToken, payloadFormatterName);
               }


               // If GetBytes fails payload really badly broken
               byte[] messageBytes = message.GetBytes();

               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} Message bytes:{messageBytes}", context.TerminalId, message.LockToken, BitConverter.ToString(messageBytes));


               // Try converting the bytes to text then to JSON
               JObject? messageJson = null;
               try
               {
                  // These will fail for some messages, payload formatter gets bytes only
                  string messageText = Encoding.UTF8.GetString(messageBytes);

                  try
                  {
                     messageJson = JObject.Parse(messageText);

                     _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} JSON:{messageJson}", context.TerminalId, message.LockToken, JsonConvert.SerializeObject(messageJson, Formatting.Indented));
                  }
                  catch (JsonReaderException jex)
                  {
                     _logger.LogInformation(jex, "Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} not valid JSON", context.TerminalId, message.LockToken);
                  }
               }
               catch (ArgumentException aex)
               {
                  _logger.LogInformation(aex, "Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} message bytes not valid text", context.TerminalId, message.LockToken);
               }


               // This shouldn't fail, but it could for invalid path to blob, timeout retrieving blob, payload formatter syntax error etc.
               IFormatterDownlink payloadFormatter = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatterName);

               // This will fail if payload formatter throws runtime exceptions like null reference, divide by zero, index out of range etc.
               byte[] payloadBytes = payloadFormatter.Evaluate(message.Properties, context.TerminalId, messageJson, messageBytes);


               // Validate payload before calling Myriota control message send API method
               if (payloadBytes is null)
               {
                  _logger.LogWarning("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} payload formatter:{payloadFormatter} Evaluate returned null", context.TerminalId, message.LockToken, payloadFormatterName);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }

               if ((payloadBytes.Length < Constants.DownlinkPayloadMinimumLength) || (payloadBytes.Length > Constants.DownlinkPayloadMaximumLength))
               {
                  _logger.LogWarning("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} PayloadBytes:{payloadBytes} length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, message.LockToken, BitConverter.ToSingle(payloadBytes), payloadBytes.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                  await context.DeviceClient.RejectAsync(message);

                  return;
               }


               // Finally send Control Message to device using the Myriota API
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} PayloadBytes:{payloadBytes} Length:{Length} sending", context.TerminalId, message.LockToken, BitConverter.ToString(payloadBytes), payloadBytes.Length);

               string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} MessageID:{messageId} sent", context.TerminalId, message.LockToken, messageId);

               await context.DeviceClient.CompleteAsync(message);
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} MessageHandler processing failed", context.TerminalId, message.LockToken);

               await context.DeviceClient.RejectAsync(message);
            }
         }
      }
   }
}

