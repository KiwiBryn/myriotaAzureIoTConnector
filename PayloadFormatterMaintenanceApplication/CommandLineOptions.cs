// Copyright (c) Septmeber 2023, devMobile Software
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
namespace devMobile.IoT.myriotaAzureIoTConnector.PayloadFormatters
{
	using CommandLine;

    public class CommandLineOptions
	{
		[Option('d', "Direction", Required = true, HelpText = "Test Uplink or DownLink formatter")]
		public string Direction { get; set; }

        [Option('p', "filename", HelpText = "Uplink or Downlink Payload file name")]
        public string PayloadFilename { get; set; } = string.Empty;

        [Option('o', "OrganisationId", Required = true, HelpText = "Organisation unique identifier")]
        public uint OrganizationId { get; set; }

        [Option('i', "DeviceId", Required = true, HelpText = "Device unique identitifer")]
        public uint DeviceId { get; set; }

        [Option('t', "DeviceType", Required = true, HelpText = "Device type number")]
        public byte DeviceType { get; set; }

        [Option('u', "UserApplicationId", Required = true, HelpText = "User Application Id")]
        public ushort UserApplicationId { get; set; }

        [Option('h', "SwarmHiveReceivedAtUtc", HelpText = "Swarm Hive received at time UTC")]
        public DateTime? SwarmHiveReceivedAtUtc { get; set; }

        [Option('w', "UplinkWebHookReceivedAtUtc", HelpText = "Webhook received at time UTC")]
        public DateTime? UplinkWebHookReceivedAtUtc { get; set; }

        [Option('s', "Status", HelpText = "Uplink local file system file name")]
        public byte? Status { get; set; }

        [Option('c', "Client", HelpText = "Uplink local file system file name")]
        public string Client { get; set; } 
    }
}