// Copyright (c) October 2023, devMobile Software, MIT License
//
[assembly: FunctionsStartup(typeof(devMobile.IoT.MyriotaAzureIoTConnector.Connector.StartUpService))]
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector;

 public class StartUpService(ILogger<StartUpService> logger, IDeviceConnectionCache deviceConnectionCache) : BackgroundService
 {
     private readonly ILogger<StartUpService> _logger = logger;
     private readonly IDeviceConnectionCache _deviceConnectionCache = deviceConnectionCache;

     protected override async Task ExecuteAsync(CancellationToken cancellationToken)
     {
         await Task.Yield();

         _logger.LogInformation("StartUpService.ExecuteAsync start");

         try
         {
             _logger.LogInformation("Myriota connection cache load start");

             await _deviceConnectionCache.TerminalListLoad(cancellationToken);

             _logger.LogInformation("Myriota connection cache load finish");
         }
         catch (Exception ex)
         {
             _logger.LogError(ex, "StartUpService.ExecuteAsync error");

             throw;
         }

         _logger.LogInformation("StartUpService.ExecuteAsync finish");
     }
 }
