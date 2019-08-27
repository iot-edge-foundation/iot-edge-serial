using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace iotedgeSerial
{

    public class SerialMessageBroadcaster
    {
        public void BroadcastMessage(string port, byte[] message)
        {
            if (BroadcastEvent != null)
            {
                BroadcastEvent(this, new SerialEventArgs { Port = port, Message = message });
            }
        }

        public event EventHandler<SerialEventArgs> BroadcastEvent;
    }
}