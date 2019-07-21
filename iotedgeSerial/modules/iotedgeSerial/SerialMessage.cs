using System;
using Newtonsoft.Json;

namespace iotedgeSerial
{
    public class SerialMessage
    {
        [JsonProperty("timestampUtc")]
        public DateTime TimestampUtc { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("device")]
        public string Device { get; set; }
    }
}