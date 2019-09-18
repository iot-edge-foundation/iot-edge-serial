using Newtonsoft.Json;

namespace iotedgeSerial
{
    public class SerialCommand
    {
        [JsonProperty("port")]
        public string Port { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }
    }
}