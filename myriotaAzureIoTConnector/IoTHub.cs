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

         _logger.LogInformation("Downlink- IoT Hub TerminalId:{termimalId} LockToken:{LockToken}", context.TerminalId, message.LockToken);

         // Use default formatter and replace with message specific formatter if configured.
         string payloadFormatter;
         if (!message.Properties.TryGetValue(Constants.IoTHubDownlinkPayloadFormatterProperty, out payloadFormatter) || string.IsNullOrEmpty(payloadFormatter))
         {
            payloadFormatter = context.PayloadFormatterDownlink;
         }

         _logger.LogInformation("Downlink- IoT Hub TerminalID:{termimalId} LockToken:{LockToken} Payload formatter:{payloadFormatter} ", context.TerminalId, message.LockToken, payloadFormatter);

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
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} messageBytes:{2} not valid Text", context.TerminalId, message.LockToken, BitConverter.ToString(messageBytes));
            }

            // This will fail for some messages, payload formatter gets bytes only
            JObject? messageJson = null;
            try
            {
               messageJson = JObject.Parse(messageText);
            }
            catch ( JsonReaderException jex)
            {
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} messageText:{2} not valid json", context.TerminalId, message.LockToken, BitConverter.ToString(messageBytes));
            }

            // This can fail for lots of diffent reasons, invalid path to blob, syntax error, interface broken etc.
            IFormatterDownlink payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatter);

            // This can fail for lots of different reasons, null references, divide by zero, out of range etc.
            byte[] payloadBytes = payloadFormatterDownlink.Evaluate(message.Properties, context.TerminalId, messageJson, messageBytes);

            // This can fail for a few reasons mainly connectivity & message queuing etc.
            _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} PayloadData:{payloadData} Length:{Length} sending", context.TerminalId, message.LockToken, Convert.ToHexString(payloadBytes), payloadBytes.Length);

            // Finally send the message using Myriota API
            string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

            _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} MessageID:{messageId} sent", context.TerminalId, message.LockToken, messageId);

            await context.DeviceClient.CompleteAsync(message);

            _logger.LogInformation("Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} MessageID:{messageId} sent", context.TerminalId, message.LockToken, messageId);
         }
         catch (Exception ex)
         {
            await context.DeviceClient.RejectAsync(message);

            _logger.LogError(ex, "Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} failed", context.TerminalId, message.LockToken);
         }
         finally
         {
            message.Dispose();
         }
      }
   }
}


/*
Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

_logger.LogInformation("Downlink- IoT Hub TerminalId:{termimalId} LockToken:{LockToken}", context.TerminalId, message.LockToken);

string payloadFormatter;

// Use default formatter and replace with message specific formatter if configured.
if (!message.Properties.TryGetValue( Constants.IoTHubDownlinkPayloadFormatterProperty, out payloadFormatter) || string.IsNullOrEmpty(payloadFormatter))
{
   payloadFormatter = context.PayloadFormatterDownlink;
}

_logger.LogInformation("Downlink- IoT Hub TerminalID:{termimalId} LockToken:{LockToken} Payload formatter:{payloadFormatter} ", context.TerminalId, message.LockToken, payloadFormatter);

try
{
   using (message)
   {        
      IFormatterDownlink payloadFormatterDownlink;

      try
      {
         payloadFormatterDownlink = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatter);
      }
      catch (CSScriptLib.CompilerException cex)
      {
         _logger.LogWarning(cex, "Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} Payload formatter:{payloadFormatter} compilation failed", context.TerminalId, message.LockToken, payloadFormatter);

         await context.DeviceClient.RejectAsync(message);

         return;
      }
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} Payload formatter:{payloadFormatter} load failed", context.TerminalId, message.LockToken, payloadFormatter);

         await context.DeviceClient.RejectAsync(message);

         return;
      }

      byte[] payloadBytes = message.GetBytes();

      string payloadText;

      try
      {
         payloadText = Encoding.UTF8.GetString(payloadBytes);
      }
      catch (ArgumentException aex)
      {
         _logger.LogInformation("Downlink-DeviceID:{DeviceId} LockToken:{LockToken} payload not valid Text", context.TerminalId, message.LockToken);
      }

      JObject? payloadJson = null;

      try
      {
         payloadJson = JObject.Parse(Encoding.UTF8.GetString(payloadBytes));
      }
      catch (JsonReaderException jex)
      {
         _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{LockToken} payload not valid JSON", context.TerminalId, message.LockToken);
      }


      byte[] payloadData;

      try
      {
         payloadData = payloadFormatterDownlink.Evaluate(message.Properties, context.TerminalId, payloadJson, payloadBytes);
      }
      catch (Exception ex) 
      {
         _logger.LogWarning(ex, "Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} payload formatter:{payloadFormatter} Evaluate failed", context.TerminalId, message.LockToken, payloadFormatter);

         await context.DeviceClient.RejectAsync(message);

         return;
      }

      if (payloadData is null)
      {
         _logger.LogWarning("Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} payload formatter:{payloadFormatter} Evaluate returned null", context.TerminalId, message.LockToken, payloadFormatter);

         await context.DeviceClient.RejectAsync(message);

         return;
      }

      if ((payloadData.Length < Constants.DownlinkPayloadMinimumLength) || (payloadData.Length > Constants.DownlinkPayloadMaximumLength))
      {
         _logger.LogWarning("Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} payloadData length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, message.LockToken, payloadData.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

         await context.DeviceClient.RejectAsync(message);

         return;
      }

      try
      {
         _logger.LogInformation("Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} PayloadData:{payloadData} Length:{Length} sending", context.TerminalId, message.LockToken, Convert.ToHexString(payloadData), payloadData.Length);

         string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadData);

         _logger.LogInformation("Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} MessageID:{messageId} sent", context.TerminalId, message.LockToken, messageId);
      }
      catch (Exception ex) // Should specialise this?
      {
         _logger.LogError(ex, "Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} Myriota SendAsync failed", context.TerminalId, message.LockToken);

         await context.DeviceClient.AbandonAsync(message);

         return;
      }

      await context.DeviceClient.CompleteAsync(message);
   }
}
catch (Exception ex)
{
   await context.DeviceClient.RejectAsync(message);

   _logger.LogError(ex, "Downlink- IoT Hub TerminalID:{terminalId} LockToken:{LockToken} payload formatter:{payloadFormatter} failed", context.TerminalId, message.LockToken, payloadFormatter);
}
*/
