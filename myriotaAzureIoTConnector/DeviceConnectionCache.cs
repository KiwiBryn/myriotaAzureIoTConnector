// Copyright (c) September 2023, devMobile Software
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
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Caching.Memory;

using LazyCache;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    public interface IDeviceConnectionCache
    {
        public Task<DeviceClient> GetOrAddAsync(string terminalId, Func<ICacheEntry, Task<DeviceClient>> addItemFactory);

        public Task<DeviceClient> GetAsync(string terminalId);
    }

    internal class DeviceConnectionCache : IDeviceConnectionCache
    {
        private static readonly IAppCache _deviceConnectionCache = new CachingService();

        public async Task<DeviceClient> GetOrAddAsync(string terminalId, Func<ICacheEntry, Task<DeviceClient>> addItemFactory)
        {
            return await _deviceConnectionCache.GetOrAddAsync(terminalId, addItemFactory, memoryCacheEntryOptions);
        }

        public async Task<DeviceClient> GetAsync(string terminalId)
        {
            return await _deviceConnectionCache.GetAsync<DeviceClient>(terminalId);
        }

        private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        {
            Priority = CacheItemPriority.NeverRemove
        };
    }
}
