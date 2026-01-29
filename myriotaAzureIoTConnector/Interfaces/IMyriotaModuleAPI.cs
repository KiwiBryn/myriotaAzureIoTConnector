// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector;

public interface IMyriotaModuleAPI
{
   public Task<Models.Item> GetAsync(string TerminalId, CancellationToken cancellationToken);

   public Task<ICollection<Models.Item>> ListAsync(CancellationToken cancellationToken);

   public Task<string> SendAsync(string terminalId, byte[] payload, CancellationToken cancellationToken = default);
}