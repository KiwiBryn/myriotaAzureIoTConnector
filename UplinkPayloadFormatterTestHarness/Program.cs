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
namespace devMobile.IoT.myriotaAzureIoTConnector.UplinkPayloadFormatterTestHarness
{
    using System;
    using System.Globalization;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using CommandLine;
    using CSScriptLib;

    internal class Program
    {
        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args)
              .WithParsed(ApplicationCore)
              .WithNotParsed(HandleParseError);

            Console.WriteLine("Press <enter> to exit");
            Console.ReadLine();
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            if (errors.IsVersion())
            {
                Console.WriteLine("Version Request");
                return;
            }

            if (errors.IsHelp())
            {
                Console.WriteLine("Help Request");
                return;
            }
            Console.WriteLine("Parser Fail");
        }

        private static void ApplicationCore(CommandLineOptions options)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            Console.WriteLine($"Uplink formatter file:{options.FormatterPath}");

            PayloadFormatter.IFormatterUplink evalulatorUplink;
            try
            {
                evalulatorUplink = CSScript.Evaluator.LoadFile<PayloadFormatter.IFormatterUplink>(options.FormatterPath);
            }
            catch (CSScriptLib.CompilerException cex)
            {
                Console.Write($"Loading or compiling file:{options.FormatterPath} failed Exception:{cex}");
                return;
            }

            byte[] payloadBytes;
            try
            {
                payloadBytes = Convert.FromHexString(options.PayloadHex);
            }
            catch (FormatException fex)
            {
                Console.WriteLine("Convert.FromHexString failed:{0}", fex.Message);
                return;
            }

            DateTime timeStamp;
            if (options.TimeStamp.HasValue)
            {
                timeStamp = options.TimeStamp.Value;
            }
            else
            {
                timeStamp = DateTime.UtcNow;
            }

            JObject telemetryEvent;

            try
            {
                telemetryEvent = evalulatorUplink.Evaluate(properties, options.Application, options.TerminalId, timeStamp, null, null, payloadBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"evalulatorUplink.Evaluate failed Exception:{ex}");
                return;
            }

            telemetryEvent.TryAdd("Application", options.Application);
            telemetryEvent.TryAdd("TerminalId", options.TerminalId);
            if ( options.TimeStamp.HasValue)
            {
                telemetryEvent.TryAdd("TimeStamp", options.TimeStamp.Value.ToString("s", CultureInfo.InvariantCulture));
            }
            telemetryEvent.TryAdd("DataLength", payloadBytes.Length);
            telemetryEvent.TryAdd("Data", Convert.ToHexString( payloadBytes));

            Console.WriteLine("Properties");
            foreach (var property in properties)
            {
                Console.WriteLine($"{property.Key}:{property.Value}");
            }

            Console.WriteLine("Payload");
            Console.WriteLine(telemetryEvent.ToString(Formatting.Indented));
        }
    }
}