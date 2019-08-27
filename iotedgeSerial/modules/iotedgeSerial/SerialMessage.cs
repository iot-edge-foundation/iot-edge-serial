using System;
using Newtonsoft.Json;

namespace iotedgeSerial
{
    public class SerialMessage
    {
        [JsonProperty("timestampUtc")]
        public DateTime TimestampUtc { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("port")]
        public string Port { get; set; }
    }
}