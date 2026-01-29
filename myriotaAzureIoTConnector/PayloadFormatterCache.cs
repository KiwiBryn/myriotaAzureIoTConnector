// Copyright (c) August 2023, devMobile Software, MIT License
//
using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector;

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
