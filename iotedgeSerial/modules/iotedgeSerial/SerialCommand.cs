using Newtonsoft.Json;

namespace iotedgeSerial
{
    public class SerialCommand
    {
        [JsonProperty("device")]
        public string Device { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; } // TODO: byte[] or string????
    }
}