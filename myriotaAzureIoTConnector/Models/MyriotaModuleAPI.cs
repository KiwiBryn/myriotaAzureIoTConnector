﻿// Copyright (c) August 2023, devMobile Software
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


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models
{
    public class Item
    {
        public string Id { get; set; }
        public Dictionary<string, string> Destinations { get; set; }
        public Dictionary<string, string> Attributes { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public int MessageCount { get; set; }
        public DateTime FirstMessageTime { get; set; }
        public DateTime LastMessageTime { get; set; }
        public DateTime RegistrationDate { get; set; }
    }

    public class ModulesResponse
    {
        public List<Item> Items { get; set; }
        public string NextItem { get; set; } = string.Empty;
    }

    public class ControlMessageSendRequest
    {
        public string ModuleId { get; set; }
        public string Message { get; set; }
    }

    public class ControlMessageSendResponse
    {
        public string Id { get; set; }
        public string ModuleId { get; set; }
        public string Status { get; set; }
    }

}
