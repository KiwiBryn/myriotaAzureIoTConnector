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
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    using System.Threading.Tasks;

    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    
    using CSScriptLib;
    using LazyCache;

    using PayloadFormatter;


    public interface IPayloadFormatterCache
    {
        public Task<IFormatterUplink> UplinkGetAsync(string userApplicationId);

        public Task<IFormatterDownlink> DownlinkGetAsync(string userApplicationId);
    }

    public class PayloadFormatterCache : IPayloadFormatterCache
    {
        private readonly ILogger<PayloadFormatterCache> _logger;
        private readonly string _payloadFormatterConnectionString;
        private readonly Models.PayloadformatterSettings _applicationSettings;
        private readonly static IAppCache _payloadFormatters = new CachingService();

        public PayloadFormatterCache(ILogger<PayloadFormatterCache>logger, IConfiguration configuration, IOptions<Models.PayloadformatterSettings> applicationSettings)
        {
            _logger = logger;
            _payloadFormatterConnectionString = configuration.GetConnectionString("PayloadFormattersStorage");
            _applicationSettings = applicationSettings.Value;
        }

        public async Task<IFormatterUplink> UplinkGetAsync(string application)
        {
            IFormatterUplink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync<PayloadFormatter.IFormatterUplink>($"U{application}", (ICacheEntry x) => UplinkLoadAsync(application), memoryCacheEntryOptions);

            return payloadFormatterUplink;
        }

        private async Task<IFormatterUplink> UplinkLoadAsync(string application)
        {
            BlobClient blobClient = new BlobClient(_payloadFormatterConnectionString, _applicationSettings.PayloadFormattersUplinkContainer, $"{application}.cs");

            if (!await blobClient.ExistsAsync())
            { 
                _logger.LogInformation("PayloadFormatterUplink- UserApplicationId:{0} Container:{1} not found using default:{2}", application, _applicationSettings.PayloadFormattersUplinkContainer, _applicationSettings.PayloadFormatterUplinkDefault);

                blobClient = new BlobClient(_payloadFormatterConnectionString, _applicationSettings.PayloadFormattersUplinkContainer, _applicationSettings.PayloadFormatterUplinkDefault);
            }

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync();

            return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterUplink>(downloadResult.Content.ToString());
        }

        public async Task<IFormatterDownlink> DownlinkGetAsync(string application)
        {
            IFormatterDownlink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync<PayloadFormatter.IFormatterDownlink>($"D{application}", (ICacheEntry x) => DownlinkLoadAsync(application), memoryCacheEntryOptions);

            return payloadFormatterUplink;
        }

        private async Task<IFormatterDownlink> DownlinkLoadAsync(string application)
        {
            BlobClient blobClient = new BlobClient(_payloadFormatterConnectionString, _applicationSettings.PayloadFormattersDownlinkContainer, $"{application}.cs");

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("PayloadFormatterDownlink- ApplicationId:{0} Container:{1} not found using default:{2}", application, _applicationSettings.PayloadFormattersUplinkContainer, _applicationSettings.PayloadFormatterDownlinkdefault);

                blobClient = new BlobClient(_payloadFormatterConnectionString, _applicationSettings.PayloadFormattersDownlinkContainer, _applicationSettings.PayloadFormatterDownlinkdefault);
            }

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync();

            return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterDownlink>(downloadResult.Content.ToString());
        }

        private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        {
            Priority = CacheItemPriority.NeverRemove
        };
    }
}
