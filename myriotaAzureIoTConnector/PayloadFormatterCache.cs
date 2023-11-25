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
   using Microsoft.Extensions.Options;

   using CSScriptLib;

   using PayloadFormatter;


   public class PayloadFormatterCache(IOptions<Models.PayloadformatterSettings> payloadformatterSettings, BlobServiceClient blobServiceClient) : IPayloadFormatterCache
   {
      private readonly Models.PayloadformatterSettings _payloadformatterSettings = payloadformatterSettings.Value;
      private readonly BlobServiceClient _blobServiceClient = blobServiceClient;

      private readonly static LazyCache.CachingService _payloadFormatters = new();

      public async Task<IFormatterUplink> UplinkGetAsync(string payloadFormatter, CancellationToken cancellationToken)
      {
         IFormatterUplink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync($"U{payloadFormatter}", (ICacheEntry x) => UplinkLoadAsync(payloadFormatter, cancellationToken), memoryCacheEntryOptions);

         return payloadFormatterUplink;
      }

      private async Task<IFormatterUplink> UplinkLoadAsync(string payloadFormatter, CancellationToken cancellationToken)
      {
         BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(_payloadformatterSettings.UplinkContainer).GetBlobClient(payloadFormatter);

         BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(cancellationToken);

         return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterUplink>(downloadResult.Content.ToString());
      }

      public async Task<IFormatterDownlink> DownlinkGetAsync(string payloadFormatter, CancellationToken cancellationToken)
      {
         IFormatterDownlink payloadFormatterUplink = await _payloadFormatters.GetOrAddAsync($"D{payloadFormatter}", (ICacheEntry x) => DownlinkLoadAsync(payloadFormatter, cancellationToken), memoryCacheEntryOptions);

         return payloadFormatterUplink;
      }

      private async Task<IFormatterDownlink> DownlinkLoadAsync(string payloadFormatter, CancellationToken cancellationToken)
      {
         BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(_payloadformatterSettings.DownlinkContainer).GetBlobClient(payloadFormatter);

         BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(cancellationToken);

         return CSScript.Evaluator.LoadCode<PayloadFormatter.IFormatterDownlink>(downloadResult.Content.ToString());
      }

      private static readonly MemoryCacheEntryOptions memoryCacheEntryOptions = new()
      {
         Priority = CacheItemPriority.NeverRemove
      };
   }
}
