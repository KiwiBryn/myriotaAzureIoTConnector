using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
    public JObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte[] payloadBytes)
    {
    }
}