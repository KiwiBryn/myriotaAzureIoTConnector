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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Extensions.DependencyInjection;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


[assembly: FunctionsStartup(typeof(devMobile.IoT.MyriotaAzureIoTConnector.Connector.StartUpService))]
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
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
}
