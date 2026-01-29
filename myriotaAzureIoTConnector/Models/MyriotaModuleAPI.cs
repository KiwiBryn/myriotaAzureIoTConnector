// Copyright (c) August 2023, devMobile Software, MIT License
//
namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector.Models;

 public class Item
 {
     public string Id { get; set; }
     public Dictionary<string, string> Destinations { get; set; }
     public Dictionary<string, string> Attributes { get; set; }
     public Dictionary<string, string> Headers { get; set; }
     public int MessageCount { get; set; }
     public DateTime FirstMessageTime { get; set; }
     public DateTime LastMessageTime { get; set; }
     public DateTime RegistrationDate { get; set; }
 }

 public class ModulesResponse
 {
     public List<Item> Items { get; set; }
     public string NextItem { get; set; } = string.Empty;
 }

 public class ControlMessageSendRequest
 {
     public string ModuleId { get; set; }
     public string Message { get; set; }
 }

 public class ControlMessageSendResponse
 {
     public string Id { get; set; }
     public string ModuleId { get; set; }
     public string Status { get; set; }
 }
