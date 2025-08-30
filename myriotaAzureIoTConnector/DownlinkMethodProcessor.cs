// Copyright (c) January 2024, devMobile Software. MIT License
//
using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
   internal class DownlinkMethodProcessor(ILogger<DownlinkMethodProcessor> _logger, IOptions<Models.AzureIoT> azureIoTSettings, IPayloadFormatterCache _payloadFormatterCache, IMyriotaModuleAPI _myriotaModuleAPI) : IDownlinkMethodProcessor
   {
      private readonly Models.AzureIoT _azureIoTSettings = azureIoTSettings.Value;

      public async Task<MethodResponse> MethodHandler(MethodRequest methodRequest, object userContext)
      {
         // DIY message identifier so processing progress can be tracked in Application Insights
         string requestId = Guid.NewGuid().ToString();

         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

         try
         {
            _logger.LogInformation("Downlink- TerminalId:{TerminalId} RequestId:{requestId} Name:{Name}", context.TerminalId, requestId, methodRequest.Name);

            // Lookup payload formatter name, none specified use context one which is from device attributes or the default in configuration
            string payloadFormatterName;
            if (_azureIoTSettings.Methods.TryGetValue(methodRequest.Name, out Models.Method? method) && !string.IsNullOrEmpty(method.Formatter))
            {
               payloadFormatterName = method.Formatter;

               _logger.LogInformation("Downlink- TerminalID:{TermimalId} RequestID:{requestId} Method formatter:{payloadFormatterName} ", context.TerminalId, requestId, payloadFormatterName);
            }
            else
            {
               payloadFormatterName = context.PayloadFormatterDownlink;

               _logger.LogInformation("Downlink- TerminalID:{TermimalId} RequestID:{requestId} Context formatter:{payloadFormatterName} ", context.TerminalId, requestId, payloadFormatterName);
            }

            // Display methodRequest.Data as Hex
            if (methodRequest.Data is not null)
            {
               _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestId} Data:{Data}", context.TerminalId, requestId, BitConverter.ToString(methodRequest.Data));
            }
            else
            {
               _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestId} Data:null", context.TerminalId, requestId);
            }


            JsonObject? methodJson = null;

            // if there is a payload try converting it...
            if ((methodRequest.Data is not null) && !string.IsNullOrWhiteSpace(methodRequest.DataAsJson) && (string.CompareOrdinal(methodRequest.DataAsJson, "null") != 0) && (string.CompareOrdinal(methodRequest.DataAsJson, "\"\"") != 0))
            {
               // The method.DataAsJson could be JSON
               try
               {
                  methodJson = JsonNode.Parse(methodRequest.DataAsJson) as JsonObject;

                  _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestId} DataAsJson:{requestJson}", context.TerminalId, requestId, JsonSerializer.Serialize(methodJson));
               }
               catch (JsonException jex)
               {
                  _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestId} DataAsJson is not valid JSON Error:{Message}", context.TerminalId, requestId, jex.Message);

                  methodJson = new JsonObject
                  {
                     { methodRequest.Name, methodRequest.DataAsJson }
                  };

                  _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestId} DataAsJson:{requestJson}", context.TerminalId, requestId, JsonSerializer.Serialize(methodJson));
               }
            }
            else
            {
               if ((method is not null) && !string.IsNullOrWhiteSpace(method.Payload))
               {
                  // If the method payload in the application configuration is broken nothing can be done
                  try
                  {
                     methodJson = JsonNode.Parse(method.Payload) as JsonObject;

                     _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestId} method payload:{requestJson}", context.TerminalId, requestId, JsonSerializer.Serialize(methodJson));
                  }
                  catch (JsonException jex)
                  {
                     _logger.LogError(jex, "Downlink- TerminalID:{TerminalId} RequestID:{requestId} Name:{methodName} invalid Method.Payload:{method.Payload}", context.TerminalId, requestId, methodRequest.Name, method.Payload);

                     return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"RequestID:{requestId} method payload invalid.\"}}"), (int)HttpStatusCode.UnprocessableEntity);
                  }
               }
            }

            // This "shouldn't" fail, but it could for invalid path to blob, timeout retrieving blob, payload formatter syntax error etc.
            IFormatterDownlink payloadFormatter = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatterName);

            if (methodJson is null)
            {
               methodJson = new JsonObject();
            }

            // This also "shouldn't" fail, but the payload formatters can throw runtime exceptions like null reference, divide by zero, index out of range etc.
            byte[] payloadBytes = payloadFormatter.Evaluate(context.TerminalId, methodRequest.Name, methodJson, methodRequest.Data);

            // Validate payload before calling Myriota control message send API method
            if (payloadBytes is null)
            {
               _logger.LogWarning("Downlink- TerminalID:{TerminalId} Request:{requestId} Evaluate returned null", context.TerminalId, requestId);

               return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"RequestID:{requestId} payload evaluate returned null.\"}}"), (int)HttpStatusCode.UnprocessableEntity);
            }

            if ((payloadBytes.Length < Constants.DownlinkPayloadMinimumLength) || (payloadBytes.Length > Constants.DownlinkPayloadMaximumLength))
            {
               _logger.LogWarning("Downlink- TerminalID:{TerminalId} RequestID:{requestId} PayloadBytes:{payloadBytes} length:{Length} invalid, must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, requestId, BitConverter.ToString(payloadBytes), payloadBytes.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

               return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"RequestID:{requestId} payload evaluation length invalid.\"}}"), (int)HttpStatusCode.UnprocessableEntity);
            }

            // Finally send Control Message to device using the Myriota API
            _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestID} PayloadBytes:{payloadBytes} Length:{Length} sending", context.TerminalId, requestId, BitConverter.ToString(payloadBytes), payloadBytes.Length);

            string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

            _logger.LogInformation("Downlink- TerminalID:{TerminalId} RequestID:{requestId} Myriota MessageID:{messageId} sent", context.TerminalId, requestId, messageId);
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Downlink- TerminalID:{TerminalId} RequestID:{requestId} IotHubMethodHandler processing failed", context.TerminalId, requestId);

            return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"TerminalID:{context.TerminalId} RequestID:{requestId} method handler failed.\"}}"), (int)HttpStatusCode.InternalServerError);
         }

         return new MethodResponse(Encoding.ASCII.GetBytes($"{{\"message\":\"TerminalID:{context.TerminalId} RequestID:{requestId} Message sent successfully.\"}}"), (int)HttpStatusCode.OK);
      }
   }
}