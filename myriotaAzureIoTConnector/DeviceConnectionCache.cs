// Copyright (c) September 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector;

internal partial class DeviceConnectionCache(ILoggerFactory loggerFactory, IOptions<Models.AzureIoT> azureIoTSettings, IOptions<Models.PayloadformatterSettings> payloadformatterSettings, IDownlinkMethodProcessor downlinkMessageProcessor, IMyriotaModuleAPI myriotaModuleAPI) : IDeviceConnectionCache
{
   private readonly ILogger _logger = loggerFactory.CreateLogger<UplinkMessageProcessor>();
   private readonly Models.AzureIoT _azureIoTSettings = azureIoTSettings.Value;
   private readonly Models.PayloadformatterSettings _payloadformatterSettings = payloadformatterSettings.Value;
   private readonly IDownlinkMethodProcessor _downlinkMessageProcessor = downlinkMessageProcessor;
   private readonly IMyriotaModuleAPI _myriotaModuleAPI = myriotaModuleAPI;

   private static readonly LazyCache.CachingService _deviceConnectionCache = new();

   public async Task<Models.DeviceConnectionContext> GetOrAddAsync(string terminalId, CancellationToken cancellationToken)
   {
      return await this.GetOrAddAsync(terminalId, null, cancellationToken);
   }

   public async Task<Models.DeviceConnectionContext> GetOrAddAsync(string terminalId, Models.Item item, CancellationToken cancellationToken)
   {
      Models.DeviceConnectionContext context;

      switch (_azureIoTSettings.ApplicationType)
      {
         case Models.ApplicationType.IoTHub:
            switch (_azureIoTSettings.IoTHub.ConnectionType)
            {
               case Models.AzureIotHubConnectionType.DeviceConnectionString:
                  context = await _deviceConnectionCache.GetOrAddAsync(terminalId, (ICacheEntry x) => DeviceConnectionStringConnectAsync(terminalId, item, cancellationToken), memoryCacheEntryOptions);
                  break;
               case Models.AzureIotHubConnectionType.DeviceProvisioningService:
                  context = await _deviceConnectionCache.GetOrAddAsync(terminalId, (ICacheEntry x) => DeviceProvisioningServiceConnectAsync(terminalId, item, _azureIoTSettings.IoTHub.DeviceProvisioningService, cancellationToken), memoryCacheEntryOptions);
                  break;
               default:
                  _logger.LogError("Uplink- Azure IoT Hub ConnectionType unknown {ConnectionType}", _azureIoTSettings.IoTHub.ConnectionType);

                  throw new NotImplementedException("AzureIoT Hub unsupported ConnectionType");
            }
            break;
         case Models.ApplicationType.IoTCentral:
            context = await _deviceConnectionCache.GetOrAddAsync(terminalId, (ICacheEntry x) => DeviceProvisioningServiceConnectAsync(terminalId, item, _azureIoTSettings.IoTCentral.DeviceProvisioningService, cancellationToken), memoryCacheEntryOptions);
            break;
         default:
            _logger.LogError("Uplink- Azure IoT ApplicationType unknown {ApplicationType}", _azureIoTSettings.ApplicationType);

            throw new NotImplementedException("AzureIoT Hub unsupported ApplicationType");
      }

      await context.DeviceClient.SetMethodDefaultHandlerAsync(_downlinkMessageProcessor.MethodHandler, context, cancellationToken);

      await context.DeviceClient.OpenAsync(cancellationToken);

      return context;
   }

   private async Task<Models.DeviceConnectionContext> DeviceConnectionStringConnectAsync(string terminalId, Models.Item item, CancellationToken cancellationToken)
   {
      item ??= await _myriotaModuleAPI.GetAsync(terminalId, cancellationToken);

      if (!item.Attributes.TryGetValue("DtdlModelId", out string? dtdlModelId))
      {
         dtdlModelId = _azureIoTSettings.DtdlModelId;
      }

      if (!item.Attributes.TryGetValue("UplinkDefault", out string? payloadFormatterUplink))
      {
         payloadFormatterUplink = _payloadformatterSettings.UplinkFormatterDefault;
      }

      if (!item.Attributes.TryGetValue("DownlinkDefault", out string? payloadFormatterDownlink))
      {
         payloadFormatterDownlink = _payloadformatterSettings.DownlinkFormatterDefault;
      }

      ClientOptions clientOptions = null;

      if (!string.IsNullOrEmpty(dtdlModelId))
      {
         clientOptions = new ClientOptions()
         {
            ModelId = dtdlModelId
         };
      }

      return new Models.DeviceConnectionContext()
      {
         TerminalId = terminalId,
         PayloadFormatterUplink = payloadFormatterUplink,
         PayloadFormatterDownlink = payloadFormatterDownlink,
         Attibutes = new Dictionary<string, string>(item.Attributes),

         DeviceClient = DeviceClient.CreateFromConnectionString(_azureIoTSettings.IoTHub.ConnectionString, terminalId, Constants.TransportSettings, clientOptions),
      };
   }

   private async Task<Models.DeviceConnectionContext> DeviceProvisioningServiceConnectAsync(string terminalId, Models.Item item, Models.AzureDeviceProvisioningService deviceProvisioningService, CancellationToken cancellationToken)
   {
      item ??= await _myriotaModuleAPI.GetAsync(terminalId, cancellationToken);

      if (!item.Attributes.TryGetValue("DtdlModelId", out string? dtdlModelId))
      {
         dtdlModelId = _azureIoTSettings.DtdlModelId;
      }

      if (!item.Attributes.TryGetValue("PayloadFormatterUplink", out string? payloadFormatterUplink))
      {
         payloadFormatterUplink = _payloadformatterSettings.UplinkFormatterDefault;
      }

      if (!item.Attributes.TryGetValue("PayloadFormatterDownlink", out string? payloadFormatterDownlink))
      {
         payloadFormatterDownlink = _payloadformatterSettings.DownlinkFormatterDefault;
      }

      string deviceKey;

      using (HMACSHA256 hmac = new(Convert.FromBase64String(deviceProvisioningService.GroupEnrollmentKey)))
      {
         deviceKey = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(terminalId)));
      }

      using (SecurityProviderSymmetricKey securityProvider = new(terminalId, deviceKey, null))
      {
         using (ProvisioningTransportHandlerAmqp transport = new(TransportFallbackType.TcpOnly))
         {
            DeviceRegistrationResult result;

            ProvisioningDeviceClient provClient = ProvisioningDeviceClient.Create(
                deviceProvisioningService.GlobalDeviceEndpoint,
                deviceProvisioningService.IdScope,
                securityProvider,
                transport);

            if (!string.IsNullOrEmpty(dtdlModelId))
            {
               ProvisioningRegistrationAdditionalData provisioningRegistrationAdditionalData = new()
               {
                  JsonData = PnpConvention.CreateDpsPayload(dtdlModelId)
               };

               result = await provClient.RegisterAsync(provisioningRegistrationAdditionalData, cancellationToken);
            }
            else
            {
               result = await provClient.RegisterAsync(cancellationToken);
            }

            if (result.Status != ProvisioningRegistrationStatusType.Assigned)
            {
               _logger.LogWarning("Uplink- TerminalId:{terminalId} RegisterAsync status:{Status} failed ", terminalId, result.Status);

               throw new ApplicationException($"Uplink- TerminalId:{terminalId} RegisterAsync status:{result.Status} failed");
            }

            IAuthenticationMethod authentication = new DeviceAuthenticationWithRegistrySymmetricKey(result.DeviceId, (securityProvider as SecurityProviderSymmetricKey).GetPrimaryKey());

            return new Models.DeviceConnectionContext()
            {
               TerminalId = terminalId,
               PayloadFormatterUplink = payloadFormatterUplink,
               PayloadFormatterDownlink = payloadFormatterDownlink,
               Attibutes = new Dictionary<string, string>(item.Attributes),

               DeviceClient = DeviceClient.Create(result.AssignedHub, authentication, Constants.TransportSettings)
            };
         }
      }
   }

   public async Task TerminalListLoad(CancellationToken cancellationToken)
   {
      foreach (Models.Item item in await _myriotaModuleAPI.ListAsync(cancellationToken))
      {
         _logger.LogInformation("Myriota TerminalId:{TerminalId} cache", item.Id);

         await this.GetOrAddAsync(item.Id, item, cancellationToken);
      }
   }

   private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new()
   {
      Priority = CacheItemPriority.NeverRemove
   };
}
