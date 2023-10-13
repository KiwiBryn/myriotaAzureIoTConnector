//---------------------------------------------------------------------------------
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
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models
{
    public enum AzureIoTApplicationType
    {
        Undefined = 0,
        IoTHub,
        IoTCentral
    }

    public class AzureIoT
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AzureIoTApplicationType ApplicationType;

        public AzureIotHub AzureIoTHub { get; set; }

        public AzureIoTCentralSetting AzureIoTCentral { get; set; }

        public string DtdlModelId { get; set; } = string.Empty;
    }

    public enum AzureIotHubConnectionType
    {
        Undefined = 0,
        DeviceConnectionString,
        DeviceProvisioningService
    }

    public class AzureDeviceProvisioningService
    {
        public string GlobalDeviceEndpoint { get; set; } = string.Empty;

        public string IdScope { get; set; } = string.Empty;

        public string GroupEnrollmentKey { get; set; } = string.Empty;
    }

    public class AzureIotHub
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public AzureIotHubConnectionType ConnectionType { get; set; }

        public string ConnectionString { get; set; } = string.Empty;

        public AzureDeviceProvisioningService DeviceProvisioningService { get; set; }
    }

    public class AzureIoTCentralSetting
    {
        public Dictionary<string, string> Methods { get; set; }
    }
}
