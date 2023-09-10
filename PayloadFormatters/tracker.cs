/*
myriota tracker payload format

typedef struct {
  uint16_t sequence_number;
  int32_t latitude;   // scaled by 1e7, e.g. -891234567 (south 89.1234567)
  int32_t longitude;  // scaled by 1e7, e.g. 1791234567 (east 179.1234567)
  uint32_t time;      // epoch timestamp of last fix
} __attribute__((packed)) tracker_message; 

*/ 
using System;
using System.Collections.Generic;
using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
    public JObject Evaluate(IDictionary<string, string> properties, string application, string terminalId, DateTime timestamp, JObject payloadJson, string payloadText, byte[] payloadBytes)
    {
        JObject telemetryEvent = new JObject();

        telemetryEvent.Add("SequenceNumber", BitConverter.ToUInt16(payloadBytes));

        double latitude = BitConverter.ToInt32(payloadBytes, 2) / 10000000.0;
        telemetryEvent.Add("Latitude", latitude);

        double longitude = BitConverter.ToInt32(payloadBytes, 6) / 10000000.0;
        telemetryEvent.Add("Longitude", longitude);

        UInt32 packetimestamp = BitConverter.ToUInt32(payloadBytes, 10);
        DateTime lastFix = DateTime.UnixEpoch.AddSeconds(packetimestamp);

        telemetryEvent.Add("LastFix", lastFix);

        properties.Add("iothub-creation-time-utc", lastFix.ToString("s", CultureInfo.InvariantCulture));

        return telemetryEvent;
    }
}