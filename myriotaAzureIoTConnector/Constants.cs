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
using Microsoft.Azure.Devices.Client;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
   internal static class Constants
   {
      public static readonly ITransportSettings[] TransportSettings = new ITransportSettings[]
      {
            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
            {
                AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                {
                    Pooling = true,
                }
             }
      };

      public const int DownlinkPayloadMinimumLength = 1;
      public const int DownlinkPayloadMaximumLength = 20;

      public const string IoTHubDownlinkPayloadFormatterProperty = "PayloadFormatter";
   }
}
