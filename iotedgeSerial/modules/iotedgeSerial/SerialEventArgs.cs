namespace iotedgeSerial
{
    using System;

    public class SerialEventArgs : EventArgs
    {
        public string Device { get; set; }
        public byte[] Message { get; set; }
    }
}