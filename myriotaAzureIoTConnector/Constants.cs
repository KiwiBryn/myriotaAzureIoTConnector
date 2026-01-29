// Copyright (c) October 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector;

internal static class Constants
{
   public static readonly ITransportSettings[] TransportSettings = new ITransportSettings[]
   {
         new AmqpTransportSettings(Microsoft.Azure.Devices.Client.TransportType.Amqp_Tcp_Only)
         {
             AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
             {
                 Pooling = true,
             }
          }
   };

   public const int DownlinkPayloadMinimumLength = 1;
   public const int DownlinkPayloadMaximumLength = 20;

   public const string IoTHubDownlinkPayloadFormatterProperty = "PayloadFormatter";
}
