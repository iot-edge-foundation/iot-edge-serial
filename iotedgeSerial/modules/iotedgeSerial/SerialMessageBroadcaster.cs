using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace iotedgeSerial
{

    public class SerialMessageBroadcaster
    {
        public void BroadcastMessage(string device, byte[] message)
        {
            if (BroadcastEvent != null)
            {
                BroadcastEvent(this, new SerialEventArgs { Device = device, Message = message });
            }
        }

        public event EventHandler<SerialEventArgs> BroadcastEvent;
    }
}