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
// Used new Visual Studio feature "Edit->Paste Special->Paste JSON as Clases" functionality
//
//---------------------------------------------------------------------------------
using System;
using System.Collections.Generic;


namespace devMobile.IoT.myriotaAzureIoTConnector.myriota.UplinkWebhook.Models
{
    public class UplinkPayloadWebDto
    {
        public string EndpointRef { get; set; }
        public ulong Timestamp { get; set; }
        public string Data { get; set; } // Embedded JSON
        public string Id { get; set; }
        public string CertificateUrl { get; set; }
        public string Signature { get; set; }
    }

    public class WebData
    {
        public List<WebPacket> Packets { get; set; }
    }

    public class WebPacket
    {
        public string TerminalId { get; set; }

        public ulong Timestamp { get; set; }

        public string Value { get; set; }
    }

    public class UplinkPayloadQueueDto
    {
        public string Application { get; set; }
        public string EndpointRef { get; set; }
        public DateTime PayloadReceivedAtUtc { get; set; }
        public DateTime PayloadArrivedAtUtc { get; set; }
        public QueueData Data { get; set; }
        public string Id { get; set; }
        public Uri CertificateUrl { get; set; }
        public string Signature { get; set; }
    }

    public class QueueData
    {
        public List<QueuePacket> Packets { get; set; }
    }

    public class QueuePacket
    {
        public string TerminalId { get; set; }

        public DateTime Timestamp { get; set; }

        public string Value { get; set; }
    }
}
