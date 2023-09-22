// Copyright (c) September 2023, devMobile Software
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
    using System;
    using System.Globalization;
    using System.Text;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using CommandLine;
    using CSScriptLib;
   
    internal class Program
    {
        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<CommandLineOptions>(args)
                .WithNotParsed(HandleParseError)
                .WithParsedAsync(ApplicationCore);

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

        private static async Task ApplicationCore(CommandLineOptions options)
        {
            switch (options.Direction.ToLower())
            {
                case "uplink":
                    await UplinkFormatterCore(options);
                    break;
                case "downlink":
                    /*
                    await DownlinkFormatterCore(options);
                    */
                    break;
                default:
                    Console.WriteLine("");
                    return;
            }
        }

        private static async Task UplinkFormatterCore(CommandLineOptions options)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            string formatterFolder = Path.Combine(Environment.CurrentDirectory, "uplink");
            Console.WriteLine($"Uplink uplinkFormatterFolder:{formatterFolder}");

            string formatterFile = Path.Combine(formatterFolder, $"{options.UserApplicationId}.cs");
            Console.WriteLine($"Uplink UserApplicationId: {options.UserApplicationId} Formatter file:{formatterFile}");

            PayloadFormatter.IFormatterUplink evalulatorUplink;
            try
            {
                evalulatorUplink = CSScript.Evaluator.LoadFile<PayloadFormatter.IFormatterUplink>(formatterFile);
            }
            catch (CSScriptLib.CompilerException cex)
            {
                Console.Write($"Loading or compiling file:{formatterFile} failed Exception:{cex}");
                return;
            }

            string payloadFilename = Path.Combine(formatterFolder, options.PayloadFilename);
            Console.WriteLine($"Uplink payloadFilename:{payloadFilename}");
            byte[] uplinkBytes;

            try
            {
                uplinkBytes = Convert.FromBase64String(File.ReadAllText(payloadFilename));
            }
            catch (DirectoryNotFoundException dex)
            {
                Console.WriteLine($"Uplink payload filename directory {formatterFolder} not found:{dex}");
                return;
            }
            catch (FileNotFoundException fnfex)
            {
                Console.WriteLine($"Uplink payload filename {payloadFilename} not found:{fnfex}");
                return;
            }
            catch (FormatException fex)
            {
                Console.WriteLine($"Uplink payload file invalid format {payloadFilename} not found:{fex}");
                return;
            }

            // See if payload can be converted to a string
            string uplinkText = string.Empty;
            try
            {
                uplinkText = Encoding.UTF8.GetString(uplinkBytes);
            }
            catch (FormatException fex)
            {
                Console.WriteLine("Encoding.UTF8.GetString failed:{0}", fex.Message);
            }

            // See if payload can be converted to JSON
            JObject uplinkJson;
            try
            {
                uplinkJson = JObject.Parse(uplinkText);
            }
            catch (JsonReaderException jrex)
            {
                Console.WriteLine("JObject.Parse failed Exception:{1}", jrex);

                uplinkJson = new JObject();
            }

            JObject telemetryEvent;

            // Transform the byte and optional text and JSON payload
            try
            {
                telemetryEvent = evalulatorUplink.Evaluate(properties, uplinkJson, packet.TerminalId, packet.Timestamp, uplinkJson, uplinkText, uplinkBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"evalulatorUplink.Evaluate failed Exception:{ex}");
                return;
            }

            telemetryEvent.TryAdd("deviceType", options.DeviceType);
            telemetryEvent.TryAdd("DeviceID", options.DeviceId);
            telemetryEvent.TryAdd("OrganizationId", options.OrganizationId);
            telemetryEvent.TryAdd("UserApplicationId", options.UserApplicationId);
            telemetryEvent.TryAdd("DataLength", uplinkBytes.Length);
            telemetryEvent.TryAdd("Data", uplinkBytes);

            // optional parameters
            if (options.SwarmHiveReceivedAtUtc.HasValue)
            {
                telemetryEvent.TryAdd("SwarmHiveReceivedAtUtc", options.SwarmHiveReceivedAtUtc.Value.ToString("s", CultureInfo.InvariantCulture));
            }
            if (options.UplinkWebHookReceivedAtUtc.HasValue)
            {
                telemetryEvent.TryAdd("UplinkWebHookReceivedAtUtc", options.UplinkWebHookReceivedAtUtc.Value.ToString("s", CultureInfo.InvariantCulture));
            }
            if (options.Status.HasValue)
            {
                telemetryEvent.TryAdd("Status", options.Status);
            }
            if (string.IsNullOrWhiteSpace(options.Client))
            {
                telemetryEvent.TryAdd("Client", options.Client);
            }

            Console.WriteLine("Properties");
            foreach (var property in properties)
            {
                Console.WriteLine($"{property.Key}:{property.Value}");
            }

            Console.WriteLine("Payload");
            Console.WriteLine(telemetryEvent.ToString(Formatting.Indented));
        }

        /*
        private static async Task DownlinkFormatterCore(CommandLineOptions options)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            string formatterFolder = Path.Combine(Environment.CurrentDirectory, "downlink");
            Console.WriteLine($"Downlink- uplinkFormatterFolder: {formatterFolder}");

            string formatterFile = Path.Combine(formatterFolder, $"{options.UserApplicationId}.cs");
            Console.WriteLine($"Downlink- UserApplicationId: {options.UserApplicationId}");
            Console.WriteLine($"Downlink- Payload formatter file: {formatterFile}");

            PayloadFormatter.IFormatterDownlink evalulator;
            try
            {
                evalulator = CSScript.Evaluator.LoadFile<PayloadFormatter.IFormatterDownlink>(formatterFile);
            }
            catch (CSScriptLib.CompilerException cex)
            {
                Console.Write($"Loading or compiling file:{formatterFile} failed Exception:{cex}");
                return;
            }

            string payloadFilename = Path.Combine(formatterFolder, options.PayloadFilename);
            Console.WriteLine($"Downlink- payloadFilename:{payloadFilename}");
            byte[] uplinkBytes;

            try
            {
                uplinkBytes = File.ReadAllBytes(payloadFilename);
            }
            catch (DirectoryNotFoundException dex)
            {
                Console.WriteLine($"Uplink payload filename directory {formatterFolder} not found:{dex}");
                return;
            }
            catch (FileNotFoundException fnfex)
            {
                Console.WriteLine($"Uplink payload filename {payloadFilename} not found:{fnfex}");
                return;
            }
            catch (FormatException fex)
            {
                Console.WriteLine($"Uplink payload file invalid format {payloadFilename} not found:{fex}");
                return;
            }

            // See if payload can be converted to a string
            string uplinkText = string.Empty;
            try
            {
                uplinkText = Encoding.UTF8.GetString(uplinkBytes);
            }
            catch (FormatException fex)
            {
                Console.WriteLine("Encoding.UTF8.GetString failed:{0}", fex.Message);
            }

            // See if payload can be converted to JSON
            JObject uplinkJson;
            try
            {
                uplinkJson = JObject.Parse(uplinkText);
            }
            catch (JsonReaderException jrex)
            {
                Console.WriteLine("JObject.Parse failed Exception:{1}", jrex);

                uplinkJson = new JObject();
            }

            Console.WriteLine("Properties");
            foreach (var property in properties)
            {
                Console.WriteLine($"{property.Key}:{property.Value}");
            }

            // Transform the byte and optional text and JSON payload
            Byte[] payload;
            try
            {
                payload = evalulator.Evaluate(properties, options.OrganizationId, options.DeviceId, options.DeviceType, options.UserApplicationId, uplinkJson, uplinkText, uplinkBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"evalulatorUplink.Evaluate failed Exception:{ex}");
                return;
            }

            Console.WriteLine("Payload");
            Console.WriteLine(Convert.ToBase64String(payload));
        }
        */
    }
}