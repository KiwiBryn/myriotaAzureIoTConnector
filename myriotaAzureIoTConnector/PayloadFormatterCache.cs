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
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using CSScriptLib;
    using LazyCache;

    using PayloadFormatter;


    public interface IPayloadFormatterCache
    {
        public Task<IFormatterUplink> UplinkGetAsync(CancellationToken cancellationToken);

        public Task<IFormatterDownlink> DownlinkGetAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

    public class PayloadFormatterCache : IPayloadFormatterCache
    {
        private readonly ILogger<PayloadFormatterCache> _logger;
        private readonly Models.PayloadformatterSettings _payloadformatterSettings;
        private readonly BlobServiceClient _blobServiceClient;

        private readonly static IAppCache _payloadFormatters = new CachingService();


        public PayloadFormatterCache(ILogger<PayloadFormatterCache>logger, IOptions<Models.PayloadformatterSettings> payloadformatterSettings, BlobServiceClient blobServiceClient)
        {
            _logger = logger;
            _payloadformatterSettings = payloadformatterSettings.Value;
            _blobServiceClient = blobServiceClient;
        }

        public async Task<IFormatterUplink> UplinkGetAsync(CancellationToken cancellationToken)
        {
            IFormatterUplink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync($"U{_payloadformatterSettings.PayloadFormatterUplinkDefault}", (ICacheEntry x) => UplinkLoadAsync(cancellationToken), memoryCacheEntryOptions);

            return payloadFormatterUplink;
        }

        private async Task<IFormatterUplink> UplinkLoadAsync(CancellationToken cancellationToken)
        {
            BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(_payloadformatterSettings.PayloadFormattersUplinkContainer).GetBlobClient(_payloadformatterSettings.PayloadFormatterUplinkDefault);

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(cancellationToken);

            return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterUplink>(downloadResult.Content.ToString());
        }

        public async Task<IFormatterDownlink> DownlinkGetAsync(CancellationToken cancellationToken)
        {
            IFormatterDownlink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync($"D{_payloadformatterSettings.PayloadFormatterDownlinkdefault}", (ICacheEntry x) => DownlinkLoadAsync( cancellationToken), memoryCacheEntryOptions);

            return payloadFormatterUplink;
        }

        private async Task<IFormatterDownlink> DownlinkLoadAsync(CancellationToken cancellationToken)
        {
            BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(_payloadformatterSettings.PayloadFormattersDownlinkContainer).GetBlobClient(_payloadformatterSettings.PayloadFormatterDownlinkdefault);

            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(cancellationToken);

            return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterDownlink>(downloadResult.Content.ToString());
        }

        private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new MemoryCacheEntryOptions()
        {
            Priority = CacheItemPriority.NeverRemove
        };
    }
}
