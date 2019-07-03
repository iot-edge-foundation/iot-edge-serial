using System;

namespace iotedgeSerial {
    public class SerialPortMessage
    {
        public DateTime PublishedUtcTimestamp { get; set; }
        public string Value { get; set; }
        public string Port { get; set; }
    }
}