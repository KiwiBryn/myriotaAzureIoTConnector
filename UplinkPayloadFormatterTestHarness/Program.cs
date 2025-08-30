// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.myriotaAzureIoTConnector.UplinkPayloadFormatterTestHarness;


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
      catch (CompilerException cex)
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

      JsonObject telemetryEvent;

      try
      {
         telemetryEvent = evalulatorUplink.Evaluate(options.TerminalId, properties, timeStamp, payloadBytes);
      }
      catch (Exception ex)
      {
         Console.WriteLine($"evalulatorUplink.Evaluate failed Exception:{ex}");
         return;
      }

      telemetryEvent.TryAdd("TerminalId", options.TerminalId);
      if (options.TimeStamp.HasValue)
      {
         telemetryEvent.TryAdd("TimeStamp", options.TimeStamp.Value.ToString("s", CultureInfo.InvariantCulture));
      }

      Console.WriteLine("Properties:");
      foreach (var property in properties)
      {
         Console.WriteLine($"{property.Key}:{property.Value}");
      }
      Console.WriteLine("");

      Console.WriteLine("JSON Telemetry event payload");
      Console.WriteLine(telemetryEvent.ToString());
   }
}