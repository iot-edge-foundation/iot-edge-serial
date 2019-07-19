using System;

namespace iotedgeSerial {
    public class SerialCommand
    {
        public string Device { get; set; } // TODO : hoofdletters?????
        public string Value { get; set; } // TODO: met dit geen byte[] zijn????
    }
}