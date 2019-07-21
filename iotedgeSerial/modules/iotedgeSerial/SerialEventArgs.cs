using System;

namespace iotedgeSerial
{
    public class SerialEventArgs : EventArgs
    {
        public string Device { get; set; }
        public byte[] Message { get; set; }
    }
}