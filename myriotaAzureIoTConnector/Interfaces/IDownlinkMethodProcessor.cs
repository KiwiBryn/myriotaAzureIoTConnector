// Copyright (c) January 2024, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
   internal interface IDownlinkMethodProcessor
   {
      public Task<MethodResponse> MethodHandler(MethodRequest methodRequest, object userContext);
   }
}
