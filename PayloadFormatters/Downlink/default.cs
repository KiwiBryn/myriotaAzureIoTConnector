using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class FormatterDownlink : PayloadFormatter.IFormatterDownlink
{
    public byte[] Evaluate(IDictionary<string, string> properties, string application, string terminalId, JObject payloadJson, byte[] payloadBytes)
    {
        return payloadBytes;
    }
}