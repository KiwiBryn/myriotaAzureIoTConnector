// Copyright (c) September 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector;

public interface IDeviceConnectionCache
{
   public Task<Models.DeviceConnectionContext> GetOrAddAsync(string terminalId, CancellationToken cancellationToken);

   public Task TerminalListLoad(CancellationToken cancellationToken);
}