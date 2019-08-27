using Newtonsoft.Json;

namespace iotedgeSerial
{
    public class SerialCommand
    {
        [JsonProperty("port")]
        public string Port { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; } // TODO: byte[] or string????
    }
}