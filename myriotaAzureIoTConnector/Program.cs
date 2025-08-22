// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
   internal class Program
   {
      static async Task Main(string[] args)
      {
         var builder = new HostBuilder();

         builder.ConfigureFunctionsWorkerDefaults()
            .ConfigureAppConfiguration(c =>
            {
               c.AddUserSecrets<Program>(optional: true, reloadOnChange: false);
               c.AddEnvironmentVariables();
            })
           .ConfigureLogging((context, l) =>
           {
              l.AddConsole();
              l.AddApplicationInsightsWebJobs(o => o.ConnectionString = context.Configuration.GetConnectionString("ApplicationInsights"));
           })
           .ConfigureServices((hostContext,services) =>
           {
              services.AddOptions<Models.AzureIoT>().Configure<IConfiguration>((settings, configuration) =>
              {
                 configuration.GetSection("AzureIoT").Bind(settings);
           });
           services.AddSingleton<IDeviceConnectionCache, DeviceConnectionCache>();
           services.AddOptions<Models.PayloadformatterSettings>().Configure<IConfiguration>((settings, configuration) =>
           {
              configuration.GetSection("PayloadFormatters").Bind(settings);
           });
           services.AddSingleton<IPayloadFormatterCache, PayloadFormatterCache>();
           services.AddSingleton<IDownlinkMethodProcessor, DownlinkMethodProcessor>();
           services.AddOptions<Models.MyriotaSettings>().Configure<IConfiguration>((settings, configuration) =>
           {
              configuration.GetSection("Myriota").Bind(settings);
           });
           services.AddSingleton<IMyriotaModuleAPI, MyriotaModuleAPI>();
           services.AddAzureClients(azureClient =>
           {
              azureClient.AddBlobServiceClient(hostContext.Configuration.GetConnectionString("PayloadFormattersStorage"));
           });
           services.AddHostedService<StartUpService>();
         })
         .UseConsoleLifetime();

         var app = builder.Build();

         using (app)
         {
            await app.RunAsync();
         }
      }
   }
}