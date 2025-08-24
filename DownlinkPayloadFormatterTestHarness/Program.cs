// Copyright (c) October 2023, devMobile Software, MIT License
//
using Newtonsoft.Json.Linq;


namespace devMobile.IoT.myriotaAzureIoTConnector.DownlinkPayloadFormatterTestHarness
{
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

            Console.WriteLine($"Downlink formatter file:{options.FormatterPath}");

            PayloadFormatter.IFormatterDownlink evaluatorDownlink;
            try
            {
                evaluatorDownlink = CSScript.Evaluator.LoadFile<PayloadFormatter.IFormatterDownlink>(options.FormatterPath);
            }
            catch (CSScriptLib.CompilerException cex)
            {
                Console.Write($"Loading or compiling file:{options.FormatterPath} failed Exception:{cex}");
                return;
            }

            byte[] payloadHex = { };

            if (!string.IsNullOrWhiteSpace(options.PayloadHex))
            {
                try
                {
                    payloadHex = Convert.FromHexString(options.PayloadHex);
                }
                catch (FormatException fex)
                {
                    Console.WriteLine("Convert.FromHexString failed:{0}", fex.Message);
                    return;
                }
            }

            JObject? payloadJson = null  ;

            if (!string.IsNullOrWhiteSpace(options.JsonPayloadPath))
            {
                Console.WriteLine($"Downlink JSON payloadFilename:{options.JsonPayloadPath}");

                try
                {
                    payloadJson = JObject.Parse(File.ReadAllText(options.JsonPayloadPath));
                }
                catch (DirectoryNotFoundException dex)
                {
                    Console.WriteLine($"Downlink payload filename directory {options.JsonPayloadPath} not found:{dex}");
                    return;
                }
                catch (FileNotFoundException fnfex)
                {
                    Console.WriteLine($"Downlink payload filename {options.JsonPayloadPath} not found:{fnfex}");
                    return;
                }
                catch (FormatException fex)
                {
                    Console.WriteLine($"Downlink payload file invalid format {options.JsonPayloadPath} not found:{fex}");
                    return;
                }
            }

            byte[] payloadBytes;
            try
            {
                payloadBytes = evaluatorDownlink.Evaluate(options.TerminalId, options.MethodName, payloadJson, payloadHex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"evalulatorDownlink.Evaluate failed Exception:{ex}");
                return;
            }

            Console.WriteLine("Properties:");
            foreach (var property in properties)
            {
                Console.WriteLine($"{property.Key}:{property.Value}");
            }
            Console.WriteLine("");

            Console.WriteLine($"Downlink payload:{Convert.ToHexString(payloadBytes)} Bytes:{payloadBytes.Length}");
        }
    }
}