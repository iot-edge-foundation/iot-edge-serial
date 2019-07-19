namespace iotedgeSerial
{
    using System;
    using System.Collections.Generic;
    using System.IO.Ports;

    public class ModuleConfig
    {
        public Dictionary<string, PortConfig> PortConfigs {get; private set;}

        public ModuleConfig(Dictionary<string, PortConfig> portConfigs)
        {
            PortConfigs = portConfigs;
        }

        public void Validate()
        {
            List<string> invalidConfigs = new List<string>();
            foreach (var config_pair in PortConfigs)
            {
                PortConfig portConfig = config_pair.Value;

                var key = config_pair.Key;

                if(portConfig == null)
                {
                    Console.WriteLine($"{key} configuration is null, remove from dictionary...");
                    invalidConfigs.Add(key);
                    continue;
                }

                if (string.IsNullOrEmpty(portConfig.device))
                {
                    Console.WriteLine($"missing device for {key}");
                    invalidConfigs.Add(key);
                }

                if (string.IsNullOrEmpty(portConfig.device))
                {
                    portConfig.direction = PortConfig.cDirection;
                    Console.WriteLine($"Invalid direction for {key}. Set to default {PortConfig.cDirection}");
                }

                if (string.IsNullOrEmpty(portConfig.direction))

                if (portConfig.sleepInterval < 1)
                {
                    portConfig.sleepInterval = PortConfig.CSleepInterval;
                    Console.WriteLine($"Invalid sleep interval for {key}. Set to default {PortConfig.CSleepInterval}");
                }

                if (portConfig.baudRate < 1)
                {
                    portConfig.baudRate = PortConfig.CBaudRate;
                    Console.WriteLine($"Invalid baudRate for {key}. Set to default {PortConfig.CBaudRate}");
                }

                if (string.IsNullOrEmpty(portConfig.parity))
                {
                    portConfig.parity = PortConfig.CParity;
                    Console.WriteLine($"Missing parity for {key}. Set to default {PortConfig.CParity}");

                    switch (portConfig.parity)
                    {
                        case "None":
                            portConfig.Parity = Parity.None;
                            break;
                        case "Even":
                            portConfig.Parity = Parity.Even;
                            break;
                        case "Odd":
                            portConfig.Parity = Parity.Odd;
                            break;
                        case "Mark":
                            portConfig.Parity = Parity.Mark;
                            break;
                        case "Space":
                            portConfig.Parity = Parity.Space;
                            break;
                    };

                }

                if (portConfig.dataBits < 1)
                {
                    portConfig.dataBits = PortConfig.CDataBits;
                    Console.WriteLine($"Invalid databits for {key}. Set to default {PortConfig.CDataBits}");
                }

                if (string.IsNullOrEmpty(portConfig.stopBits))
                {
                    portConfig.stopBits = PortConfig.CStopBits;
                    Console.WriteLine($"Missing stopBits for {key}. Set to default {PortConfig.CStopBits}");

                    switch (portConfig.stopBits)
                    {
                        case "None":
                            portConfig.StopBits = StopBits.None;
                            break;
                        case "One":
                            portConfig.StopBits = StopBits.One;
                            break;
                        case "OnePointFive":
                            portConfig.StopBits = StopBits.OnePointFive;
                            break;
                        case "Two":
                            portConfig.StopBits = StopBits.Two;
                            break;
                    };
                }

                if (string.IsNullOrEmpty(portConfig.delimiter))
                {
                    portConfig.delimiter = PortConfig.CDelimiter;
                    Console.WriteLine($"Missing delimiter for {key}. Set to defailt {PortConfig.CDelimiter}");
                }
            }

            foreach(var in_slave in invalidConfigs)
            {
                PortConfigs.Remove(in_slave);
            }
        }
    }

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