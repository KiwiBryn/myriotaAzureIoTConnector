/*
Copyright (c) August 2025, devMobile Software, MIT License

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
using System.Text.Json.Nodes;


public class FormatterUplink : PayloadFormatter.IFormatterUplink
{
    public JsonObject Evaluate(string terminalId, IDictionary<string, string> properties, DateTime timestamp, byte[] payloadBytes)
    {
      JsonObject telemetryEvent = new JsonObject();

        if (payloadBytes is null)
        {
            return telemetryEvent;
        }

        telemetryEvent.Add("SequenceNumber", BitConverter.ToUInt16(payloadBytes));

        JsonObject location = new JsonObject();

        double latitude = BitConverter.ToInt32(payloadBytes, 2) / 10000000.0;
        location.Add("lat", latitude);

        double longitude = BitConverter.ToInt32(payloadBytes, 6) / 10000000.0;
        location.Add("lon", longitude);

        location.Add("alt", 0);

        telemetryEvent.Add("DeviceLocation", location);

        UInt32 packetimestamp = BitConverter.ToUInt32(payloadBytes, 10);

        DateTime fixAtUtc = DateTime.UnixEpoch.AddSeconds(packetimestamp);

        telemetryEvent.Add("FixAtUtc", fixAtUtc);

        properties.Add("iothub-creation-time-utc", fixAtUtc.ToString("s", CultureInfo.InvariantCulture));

        return telemetryEvent;
    }
}