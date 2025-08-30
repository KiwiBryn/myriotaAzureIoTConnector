// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models
{
   public enum ApplicationType
   {
      Undefined = 0,
      IoTHub,
      IoTCentral
   }

   public class AzureIoT
   {
      [JsonConverter(typeof(JsonStringEnumConverter))]
      public ApplicationType ApplicationType { get; set; }

      public AzureIotHub IoTHub { get; set; }

      public AzureIoTCentral IoTCentral { get; set; }

      public CaseInsensitiveDictionary<Method> Methods { get; set; }

      public string DtdlModelId { get; set; } = string.Empty;
   }

   public enum AzureIotHubConnectionType
   {
      Undefined = 0,
      DeviceConnectionString,
      DeviceProvisioningService
   }

   public class CaseInsensitiveDictionary<T> : Dictionary<string, T>
   {
      public CaseInsensitiveDictionary() : base(StringComparer.OrdinalIgnoreCase)
      {
      }
   }

   public class AzureIotHub
   {
      [JsonConverter(typeof(JsonStringEnumConverter))]
      public AzureIotHubConnectionType ConnectionType { get; set; }

      public string ConnectionString { get; set; } = string.Empty;

      public AzureDeviceProvisioningService DeviceProvisioningService { get; set; }
   }

   public class Method
   {
      public string Formatter { get; set; } = string.Empty;

      public string Payload { get; set; } = string.Empty;
   }

   public class AzureIoTCentral
   {
      public AzureDeviceProvisioningService DeviceProvisioningService { get; set; }
   }

   public class AzureDeviceProvisioningService
   {
      public string GlobalDeviceEndpoint { get; set; } = string.Empty;

      public string IdScope { get; set; } = string.Empty;

      public string GroupEnrollmentKey { get; set; } = string.Empty;
   }
}
