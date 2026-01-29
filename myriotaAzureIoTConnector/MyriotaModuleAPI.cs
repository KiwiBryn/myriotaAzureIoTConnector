// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector;

internal class MyriotaModuleAPI(IOptions<Models.MyriotaSettings> myiotaSettings) : IMyriotaModuleAPI
{
   private readonly Models.MyriotaSettings _myiotaSettings = myiotaSettings.Value;

   public async Task<Models.Item> GetAsync(string TerminalId, CancellationToken cancellationToken)
   {
      RestClientOptions restClientOptions = new()
      {
         BaseUrl = new Uri(_myiotaSettings.BaseUrl),
         ThrowOnAnyError = true,
      };

      using (RestClient client = new(restClientOptions))
      {
         RestRequest request = new($"v1/modules/{TerminalId}?Destinations=false");

         request.AddHeader("Authorization", _myiotaSettings.ApiToken);

         Models.ModulesResponse? response = await client.GetAsync<Models.ModulesResponse>(request, cancellationToken);

         if ((response is null) || (response.Items is null) || (response.Items.Count != 1)) 
         {
            throw new ApplicationException($"MyriotaModuleAPI - GetAsync module:{TerminalId} not found");
         }

         return response.Items[0];
      }
   }

   public async Task<ICollection<Models.Item>> ListAsync(CancellationToken cancellationToken)
   {
      RestClientOptions restClientOptions = new()
      {
         BaseUrl = new Uri(_myiotaSettings.BaseUrl),
         ThrowOnAnyError = true,
      };

      // Need to rewrite this to handle paging
      using (RestClient client = new(restClientOptions))
      {
         RestRequest request = new("v1/modules?Destinations=false");

         request.AddHeader("Authorization", _myiotaSettings.ApiToken);

         Models.ModulesResponse? response = await client.GetAsync<Models.ModulesResponse>(request, cancellationToken);

         if ((response is null) || (response.Items is null))
         {
            throw new ApplicationException($"MyriotaModuleAPI - GetAsync no module list returned");
         }

         return response.Items;
      }
   }

   public async Task<string> SendAsync(string terminalId, byte[] payload, CancellationToken cancellationToken = default)
   {
      Models.ControlMessageSendRequest message = new()
      {
         ModuleId = terminalId,
         Message = Convert.ToHexString(payload),
      };

      RestClientOptions restClientOptions = new()
      {
         BaseUrl = new Uri(_myiotaSettings.BaseUrl),
         ThrowOnAnyError = true,
      };

      if (_myiotaSettings.DownlinkEnabled)
      {
         using (RestClient client = new(restClientOptions))
         {
            RestRequest request = new("v1/control-messages", Method.Post);

            request.AddJsonBody(JsonSerializer.Serialize(message));

            request.AddHeader("Authorization", _myiotaSettings.ApiToken);

            Models.ControlMessageSendResponse? sendResponse = await client.PostAsync<Models.ControlMessageSendResponse>(request, cancellationToken);

            if (sendResponse is not null)
            {
               return sendResponse.Id;
            }
         }
      }

      return string.Empty;
   }
}
