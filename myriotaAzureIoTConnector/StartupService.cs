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
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(devMobile.IoT.MyriotaAzureIoTConnector.Connector.StartUpService))]
 namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
 
 
    public class StartUpService : BackgroundService
    {
        private readonly ILogger<StartUpService> _logger;
        private readonly IDeviceConnectionCache _deviceConnectionCache;
        private readonly IMyriotaModuleAPI _myriotaModuleAPI;

        public StartUpService(ILogger<StartUpService> logger, IDeviceConnectionCache deviceConnectionCache, IMyriotaModuleAPI myriotaModuleAPI)
        {
            _logger = logger;
            _deviceConnectionCache = deviceConnectionCache;
            _myriotaModuleAPI = myriotaModuleAPI;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();

            _logger.LogInformation("StartUpService.ExecuteAsync start");

            try
            {
                _logger.LogInformation("Myriota connection cache load start");

                foreach (Models.Item item in await _myriotaModuleAPI.ListAsync(cancellationToken))
                {
                    _logger.LogInformation("Myriota TerminalId:{TerminalId}", item.Id);

                   await _deviceConnectionCache.GetOrAddAsync(item.Id,cancellationToken);
                }

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
}
