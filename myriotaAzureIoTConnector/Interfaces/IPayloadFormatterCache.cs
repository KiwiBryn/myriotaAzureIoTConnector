// Copyright (c) October 2023, devMobile Software, MIT License
//
using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    public interface IPayloadFormatterCache
   {
      public Task<IFormatterUplink> UplinkGetAsync(string payloadFormatter, CancellationToken cancellationToken);

      public Task<IFormatterDownlink> DownlinkGetAsync(string payloadFormatter, CancellationToken cancellationToken = default(CancellationToken));
   }
}
