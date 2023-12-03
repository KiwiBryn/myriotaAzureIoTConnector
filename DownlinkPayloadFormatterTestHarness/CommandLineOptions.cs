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
namespace devMobile.IoT.myriotaAzureIoTConnector.DownlinkPayloadFormatterTestHarness
{
    using CommandLine;

    public class CommandLineOptions
    {
        [Option('f', "Formatter", Required = true, HelpText = "payload formatter path")]
        public string FormatterPath { get; set; }

        [Option('t', "TerminalId", Required = true, HelpText = "Terminal unique identitifer")]
        public string TerminalId { get; set; }

        [Option('m', "MethodName", Required = true, HelpText = "Method name")]
        public string MethodName { get; set; }

        [Option('j', "JSONFile", Required = false, HelpText = "JSON payload file path")]
        public string JsonPayloadPath { get; set; }

        [Option('h', "PayloadHex", Required = false, HelpText = "Hex payload")]
        public string PayloadHex { get; set; }
    }
}