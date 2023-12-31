﻿//---------------------------------------------------------------------------------
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
using System.Collections.Generic;

using Microsoft.Azure.Devices.Client;

namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models
{
   public class DeviceConnectionContext
   {
      public string TerminalId { get; set; } = string.Empty;

      public string ModuleType { get; set; } = string.Empty;

      public string PayloadFormatterUplink { get; set; } = string.Empty;

      public string PayloadFormatterDownlink { get; set; } = string.Empty;

      public Dictionary<string, string> Attibutes { get; set; }

      public DeviceClient DeviceClient { get; set; }
   }
}
