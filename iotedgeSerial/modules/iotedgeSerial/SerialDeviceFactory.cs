namespace iotedgeSerial
{
    using System.IO.Ports;

    public static class SerialDeviceFactory
    {
        public static ISerialDevice CreateSerialDevice(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            ISerialDevice device = WinSerialDevice.CreateDevice(portName, baudRate, parity, dataBits, stopBits);
            return device;
        }
    }
}