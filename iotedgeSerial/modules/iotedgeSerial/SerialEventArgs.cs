using System;

namespace iotedgeSerial
{
    public class SerialEventArgs : EventArgs
    {
        public string Port { get; set; }
        public byte[] Message { get; set; }
    }
}