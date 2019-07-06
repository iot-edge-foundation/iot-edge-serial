using System;

namespace iotedgeSerial {
    public class SerialMessage
    {
        public DateTime TimestampUtc { get; set; }
        public string Data { get; set; }
        public string Device { get; set; }
    }
}