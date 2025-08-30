// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.myriotaAzureIoTConnector.DownlinkPayloadFormatterTestHarness;


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
      Console.WriteLine($"Downlink formatter file:{options.FormatterPath}");

      PayloadFormatter.IFormatterDownlink evaluatorDownlink;
      try
      {
         evaluatorDownlink = CSScript.Evaluator.LoadFile<PayloadFormatter.IFormatterDownlink>(options.FormatterPath);
      }
      catch (CompilerException cex)
      {
         Console.Write($"Loading or compiling file:{options.FormatterPath} failed Exception:{cex}");
         return;
      }

      byte[] payloadHex;

      try
      {
         payloadHex = Convert.FromHexString(options.PayloadHex);
      }
      catch (FormatException fex)
      {
         Console.WriteLine("Convert.FromHexString failed:{0}", fex.Message);
         return;
      }

      JsonObject? payloadJson;

      try
      {
         payloadJson = JsonNode.Parse(File.ReadAllText(options.JsonPayloadPath)) as JsonObject;
      }
      catch (FormatException fex)
      {
         Console.WriteLine($"Downlink payload file invalid format {options.JsonPayloadPath} not found:{fex}");
         return;
      }

      try
      {
         byte[] payloadBytes = evaluatorDownlink.Evaluate(options.TerminalId, options.MethodName, payloadJson, payloadHex);

         Console.WriteLine($"Downlink payload:{Convert.ToHexString(payloadBytes)} Bytes:{payloadBytes.Length}");
      }
      catch (Exception ex)
      {
         Console.WriteLine($"evalulatorDownlink.Evaluate failed Exception:{ex}");
      }
   }
}