/*
Copyright (c) August 2025, devMobile Software, MIT License

// Location entry with latitude, longitude, and timestamp. Values are little endian.
typedef struct {
  int32_t latitude;   // scaled by 1e7, e.g. -891234567 (south 89.1234567)
  int32_t longitude;  // scaled by 1e7, e.g. 1791234567 (east 179.1234567)
  uint32_t time;      // epoch timestamp of location record
} __attribute__((packed)) location_t;

// Format of the messages to be transmitted.
typedef struct {
  uint16_t sequence_number;  // Sequence number of the message
  uint8_t location_count;    // Number of location entries in the below array
  location_t locations[LOCATIONS_PER_MESSAGE];  // Array of location entries

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

      double latitude = 0.0;
      double longitude = 0.0;
      double altitude = 0.0;
      DateTime fixAtUtc = DateTime.UtcNow;

      byte locationCount = payloadBytes[2];

      JsonArray locations = new JsonArray();

      for (int index = 0; index < locationCount; index++)
      {
         JsonObject location = new JsonObject();

         int offset = (index * 12) + 3;

         latitude = BitConverter.ToInt32(payloadBytes, offset) / 10000000.0;
         location.Add("lat", latitude);

         longitude = BitConverter.ToInt32(payloadBytes, offset + 4) / 10000000.0;
         location.Add("lon", longitude);

         location.Add("alt", altitude);

         UInt32 packetimestamp = BitConverter.ToUInt32(payloadBytes, offset + 8);

         fixAtUtc = DateTime.UnixEpoch.AddSeconds(packetimestamp);

         location.Add("FixAtUtc", fixAtUtc);

         locations.Add(location);
      }

      telemetryEvent.Add("Locations", locations);

      JsonObject deviceLocationLatest = new JsonObject();

      deviceLocationLatest.Add("lat", latitude);
      deviceLocationLatest.Add("lon", longitude);
      deviceLocationLatest.Add("alt", altitude);

      telemetryEvent.Add("DeviceLocation", deviceLocationLatest);
      

      JsonObject trackingLatest = new JsonObject();
      trackingLatest.Add("lat", latitude);
      trackingLatest.Add("lon", longitude);
      trackingLatest.Add("alt", altitude);

      telemetryEvent.Add("Tracking", trackingLatest);
      telemetryEvent.Add("FixAtUtc", fixAtUtc);

      properties.Add("iothub-creation-time-utc", fixAtUtc.ToString("s", CultureInfo.InvariantCulture));

      return telemetryEvent;
   }
}