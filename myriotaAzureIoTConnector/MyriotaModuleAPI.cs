// Copyright (c) October 2023, devMobile Software
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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using RestSharp;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
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
               RestRequest request = new("v1/control-messages/", Method.Post);

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
}
