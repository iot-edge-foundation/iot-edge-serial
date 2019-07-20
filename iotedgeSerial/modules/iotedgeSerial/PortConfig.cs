namespace iotedgeSerial
{
    using System.IO.Ports;
    using Newtonsoft.Json;

    public class PortConfig
    {
        
        [JsonProperty("direction")]
        public string Direction {get; set;}
        
        [JsonProperty("device")]    
        public string Device {get; set;}
        
        [JsonProperty("sleepInterval")] 
        public int SleepInterval {get; set;} 
        
        [JsonProperty("baudRate")] 
        public int BaudRate {get; set;} 
        
        [JsonProperty("parity")] 
        public string Parity {get; set;}

        public Parity ParityEnum {get; set;}

        [JsonProperty("dataBits")] 
        public int DataBits {get; set;} 
        
        [JsonProperty("stopBits")] 
        public string StopBits {get; set;} 

        public StopBits StopBitsEnum {get; set;} 

        [JsonProperty("delimiter")] 
        public string Delimiter {get; set;} 
        
        [JsonProperty("ignoreEmptyLines")] 
        public bool IgnoreEmptyLines {get; set;} 
    }
}