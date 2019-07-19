namespace iotedgeSerial
{
    using System.IO.Ports;

    public class PortConfig
    {
        public const string cDirection = "Read";
        public const string CDevice = "/dev/ttyS0";
        public const int CSleepInterval  = 10;
        public const int CBaudRate = 9600;
        public const string CParity = "None";
        public const int CDataBits =  8;
        public const string CStopBits = "One";
        public const string CDelimiter =  "\r\n";
        public const bool CIgnoreEmptyLines  = true;

        public string direction {get; set;}
        public string device {get; set;}
        public int sleepInterval {get; set;} 
        public int baudRate {get; set;} 
        public string parity {get; set;}

        public Parity Parity {get; set;}

        public int dataBits {get; set;} 
        public string stopBits {get; set;} 

        public StopBits StopBits {get; set;} 

        public string delimiter {get; set;} 
        public bool ignoreEmptyLines {get; set;} 
    }
}