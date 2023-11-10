// Copyright (c) August 2023, devMobile Software
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
using System.Threading.Tasks;

using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


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
           services.AddSingleton<IIoTHubDownlink, IoTHubDownlink>();
           services.AddSingleton<IIoTCentralDownlink, IoTCentralDownlink>();
           services.AddOptions<Models.MyriotaSettings>().Configure<IConfiguration>((settings, configuration) =>
           {
              configuration.GetSection("Myriota").Bind(settings);
           });
           services.AddSingleton<IMyriotaModuleAPI, MyriotaModuleAPI>();
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