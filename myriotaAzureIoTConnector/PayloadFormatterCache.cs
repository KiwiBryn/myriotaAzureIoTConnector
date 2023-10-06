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
    using System.Threading;
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
        public Task<IFormatterUplink> UplinkGetAsync(string application, CancellationToken cancellationToken);

        public Task<IFormatterDownlink> DownlinkGetAsync(string application, CancellationToken cancellationToken = default(CancellationToken));
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

        public async Task<IFormatterUplink> UplinkGetAsync(string application,CancellationToken cancellationToken)
        {
            IFormatterUplink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync<PayloadFormatter.IFormatterUplink>($"U{application}", (ICacheEntry x) => UplinkLoadAsync(application, cancellationToken), memoryCacheEntryOptions);

            return payloadFormatterUplink;
        }

        private async Task<IFormatterUplink> UplinkLoadAsync(string application, CancellationToken cancellationToken)
        {
            string containerName = _applicationSettings.PayloadFormattersUplinkContainer.ToLower();
            string blobName = $"{application.ToLower()}.cs";
            string blobNameDefault = _applicationSettings.PayloadFormatterUplinkDefault.ToLower();

            BlobClient blobClient = new BlobClient(_payloadFormatterConnectionString, containerName, blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            { 
                _logger.LogInformation("PayloadFormatterUplink- Application formatter:{0} Container:{1} not found using default:{2}", blobName, containerName, blobNameDefault);

                blobClient = new BlobClient(_payloadFormatterConnectionString, containerName, blobNameDefault);
            }

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(cancellationToken);

            return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterUplink>(downloadResult.Content.ToString());
        }

        public async Task<IFormatterDownlink> DownlinkGetAsync(string application, CancellationToken cancellationToken)
        {
            IFormatterDownlink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync<PayloadFormatter.IFormatterDownlink>($"D{application}", (ICacheEntry x) => DownlinkLoadAsync(application, cancellationToken), memoryCacheEntryOptions);

            return payloadFormatterUplink;
        }

        private async Task<IFormatterDownlink> DownlinkLoadAsync(string application, CancellationToken cancellationToken)
        {
            string containerName = _applicationSettings.PayloadFormattersDownlinkContainer.ToLower();
            string blobName = $"{application.ToLower()}.cs";
            string blobNameDefault = _applicationSettings.PayloadFormatterDownlinkdefault.ToLower();

            BlobClient blobClient = new BlobClient(_payloadFormatterConnectionString, containerName, blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogInformation("PayloadFormatterDownlink- Application formatter:{0} Container:{1} not found using default:{2}", application, containerName, blobNameDefault);

                blobClient = new BlobClient(_payloadFormatterConnectionString, containerName, blobNameDefault);
            }

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(cancellationToken);

            return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterDownlink>(downloadResult.Content.ToString());
        }

        private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        {
            Priority = CacheItemPriority.NeverRemove
        };
    }
}
