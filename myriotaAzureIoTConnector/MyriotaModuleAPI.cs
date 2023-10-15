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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RestSharp;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
    public interface IMyriotaModuleAPI
    {
        public Task<Models.Item> GetAsync(string TerminalId, CancellationToken cancellationToken);

        public Task<ICollection<Models.Item>> ListAsync(CancellationToken cancellationToken);

        public Task<string> SendAsync(string terminalId, byte[] payload, CancellationToken cancellationToken = default);
    }

    internal class MyriotaModuleAPI : IMyriotaModuleAPI
    {
        private readonly ILogger<MyriotaModuleAPI> _logger;
        private readonly Models.MyriotaSettings _myiotaSettings;

        public MyriotaModuleAPI(ILogger<MyriotaModuleAPI> logger, IOptions<Models.MyriotaSettings> myiotaSettings)
        {
            _logger = logger;
            _myiotaSettings = myiotaSettings.Value;
        }

        public async Task<Models.Item> GetAsync(string TerminalId, CancellationToken cancellationToken)
        {
            RestClientOptions restClientOptions = new RestClientOptions()
            {
                BaseUrl = new Uri(_myiotaSettings.BaseUrl),
                ThrowOnAnyError = true,
            };

            // Need to rewrite this to handle paging
            using (RestClient client = new RestClient(restClientOptions))
            {
                RestRequest request = new RestRequest($"v1/modules/{TerminalId}?Destinations=false");

                request.AddHeader("Authorization", _myiotaSettings.ApiToken);

                Models.ModulesResponse response = await client.GetAsync<Models.ModulesResponse>(request, cancellationToken);

                return response.Items[0];
            }
        }

        public async Task<ICollection<Models.Item>> ListAsync(CancellationToken cancellationToken)
        {
            RestClientOptions restClientOptions = new RestClientOptions()
            {
                BaseUrl = new Uri(_myiotaSettings.BaseUrl),
                ThrowOnAnyError = true,
            };

            // Need to rewrite this to handle paging
            using (RestClient client = new RestClient(restClientOptions))
            {
                RestRequest request = new RestRequest("v1/modules?Destinations=false");

                request.AddHeader("Authorization", _myiotaSettings.ApiToken);

                Models.ModulesResponse response = await client.GetAsync<Models.ModulesResponse>(request, cancellationToken);

                return response.Items;
            }
        }

        public async Task<String> SendAsync(string terminalId, byte[] payload, CancellationToken cancellationToken = default)
        {
            Models.ControlMessageSendRequest sendRequest = new Models.ControlMessageSendRequest()
            {
                ModuleId = terminalId,
                Message = Convert.ToBase64String(payload),
            };

            RestClientOptions restClientOptions = new RestClientOptions()
            {
                BaseUrl = new Uri(_myiotaSettings.BaseUrl),
                ThrowOnAnyError = true,
            };

            if (_myiotaSettings.DownlinkEnabled)
            {
                using (RestClient client = new RestClient(restClientOptions))
                {
                    RestRequest request = new RestRequest("v1/control-messages/", Method.Post);

                    request.AddBody(sendRequest);

                    request.AddHeader("Authorization", _myiotaSettings.ApiToken);

                    Models.ControlMessageSendResponse sendResponse = await client.PostAsync<Models.ControlMessageSendResponse>(request, cancellationToken);

                    return sendResponse.Id;
                }
            }

            return string.Empty;
        }
    }
}
