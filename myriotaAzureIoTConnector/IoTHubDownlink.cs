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
using System.Net;
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
   internal class IoTHubDownlink(ILogger<IoTHubDownlink> _logger, IOptions<Models.AzureIoT> azureIoTSettings, IPayloadFormatterCache _payloadFormatterCache, IMyriotaModuleAPI _myriotaModuleAPI) : IIoTHubDownlink
   {
      private readonly Models.AzureIoT _azureIoTSettings = azureIoTSettings.Value;

      public async Task<MethodResponse> IotHubMethodHandler(MethodRequest methodRequest, object userContext)
      {
         // DIY request identifier so processing progress can be tracked in Application Insights
         string requestId = Guid.NewGuid().ToString();

         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

         try
         {
            _logger.LogInformation("Downlink- TerminalId:{TerminalId} RequestId:{requestId} Name:{Name}", context.TerminalId, requestId, methodRequest.Name);

            // Lookup payload formatter name, none specified use context one which is from device attributes or the default in configuration
            string payloadFormatterName;
            if (_azureIoTSettings.IoTHub.Methods.TryGetValue(methodRequest.Name, out Models.AzureIoTHubMethod? method) && !string.IsNullOrEmpty(method.Formatter))
            {
               payloadFormatterName = method.Formatter;

               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TermimalId} RequestID:{requestId} Method formatter:{payloadFormatterName} ", context.TerminalId, requestId, payloadFormatterName);
            }
            else
            {
               payloadFormatterName = context.PayloadFormatterDownlink;

               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TermimalId} RequestID:{requestId} Context formatter:{payloadFormatterName} ", context.TerminalId, requestId, payloadFormatterName);
            }

            // Display methodRequest.Data as Hex
            if (methodRequest.Data is not null)
            {
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} Data:{Data}", context.TerminalId, requestId, BitConverter.ToString(methodRequest.Data));
            }
            else
            {
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} Data:null", context.TerminalId, requestId);
            }

            JObject? requestJson = null;

            // if there is a payload try converting it...
            if ((methodRequest.Data is not null) && !string.IsNullOrWhiteSpace(methodRequest.DataAsJson) && (string.CompareOrdinal(methodRequest.DataAsJson, "null") != 0))
            {
               // The method.DataAsJson could be JSON
               try
               {
                  requestJson = JObject.Parse(methodRequest.DataAsJson);

                  _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} DataAsJson:{requestJson}", context.TerminalId, requestId, JsonConvert.SerializeObject(requestJson, Formatting.Indented));
               }
               catch (JsonReaderException jex)
               {
                  _logger.LogInformation(jex, "Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} DataAsJson is not valid JSON", context.TerminalId, requestId);
               }
            }
            else
            {
               if ((method is not null) && !string.IsNullOrWhiteSpace(method.Payload))
               {
                  // The method.payload could be JSON
                  try
                  {
                     requestJson = JObject.Parse(method.Payload);

                     _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} DataAsJson:{requestJson}", context.TerminalId, requestId, JsonConvert.SerializeObject(requestJson, Formatting.Indented));
                  }
                  catch (JsonReaderException jex)
                  {
                     _logger.LogInformation(jex, "Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} DataAsJson is not valid JSON", context.TerminalId, requestId);
                  }
               }
            }

            // This "shouldn't" fail, but it could for invalid path to blob, timeout retrieving blob, payload formatter syntax error etc.
            IFormatterDownlink payloadFormatter = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatterName);

            if (requestJson is null)
            {
               requestJson = new JObject();
            }

            // This also "shouldn't" fail, but the payload formatters can throw runtime exceptions like null reference, divide by zero, index out of range etc.
            byte[] payloadBytes = payloadFormatter.Evaluate(context.TerminalId, methodRequest.Name, requestJson, methodRequest.Data);

            // Validate payload before calling Myriota control message send API method
            if (payloadBytes is null)
            {
               _logger.LogWarning("Downlink- IoT Hub TerminalID:{TerminalId} Request:{requestId} Evaluate returned null", context.TerminalId, requestId);

               return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"RequestID:{requestId} payload evaluate returned null.\"}}"), (int)HttpStatusCode.UnprocessableEntity);
            }

            if ((payloadBytes.Length < Constants.DownlinkPayloadMinimumLength) || (payloadBytes.Length > Constants.DownlinkPayloadMaximumLength))
            {
               _logger.LogWarning("Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} PayloadBytes:{payloadBytes} length:{Length} invalid, must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, requestId, BitConverter.ToString(payloadBytes), payloadBytes.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength); ;

               return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"RequestID:{requestId} payload evaluation length invalid.\"}}"), (int)HttpStatusCode.UnprocessableEntity);
            }

            // Finally send Control Message to device using the Myriota API
            _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestID} PayloadBytes:{payloadBytes} Length:{Length} sending", context.TerminalId, requestId, BitConverter.ToString(payloadBytes), payloadBytes.Length);

            string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

            _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} Myriota MessageID:{messageId} sent", context.TerminalId, requestId, messageId);
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Downlink- IoT Hub TerminalID:{TerminalId} RequestID:{requestId} IotHubMethodHandler processing failed", context.TerminalId, requestId);

            return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"TerminalID:{context.TerminalId} RequestID:{requestId} method handler failed.\"}}"), (int)HttpStatusCode.InternalServerError);
         }

         return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"TerminalID:{context.TerminalId} RequestID:{requestId} Message sent successfully.\"}}"), (int)HttpStatusCode.OK);
      }
   }
}

