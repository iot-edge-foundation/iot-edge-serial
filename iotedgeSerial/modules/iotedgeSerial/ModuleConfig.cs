using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace iotedgeSerial
{
    public class ModuleConfig
    {
        public Dictionary<string, PortConfig> PortConfigs { get; private set; }

        public ModuleConfig(Dictionary<string, PortConfig> portConfigs)
        {
            PortConfigs = portConfigs;
        }

        private const string DefaultDirection = "Read";
        private const string DefaultDevice = "/dev/ttyS0";

        private const int DefaultSleepInterval = 10;
        private const int DefaultBaudRate = 9600;
        private const string DefaultParity = "None";
        private const int DefaultDataBits = 8;
        private const string DefaultStopBits = "One";
        private const string DefaultDelimiter = "\r\n";
        private const bool DefaultIgnoreEmptyLines = true;

        public void Validate()
        {
            List<string> invalidConfigs = new List<string>();
            foreach (var config_pair in PortConfigs)
            {
                PortConfig portConfig = config_pair.Value;

                var key = config_pair.Key;

                if (portConfig == null)
                {
                    Console.WriteLine($"{key} configuration is null, remove from dictionary...");
                    invalidConfigs.Add(key);
                    continue;
                }

                if (string.IsNullOrEmpty(portConfig.Device))
                {
                    Console.WriteLine($"missing device for {key}");
                    invalidConfigs.Add(key);
                }

                if (string.IsNullOrEmpty(portConfig.Direction))
                {
                    portConfig.Direction = DefaultDirection;
                    Console.WriteLine($"Invalid direction for {key}. Set to default {DefaultDirection}");
                }

                if (portConfig.SleepInterval < 1)
                {
                    portConfig.SleepInterval = DefaultSleepInterval;
                    Console.WriteLine($"Invalid sleep interval for {key}. Set to default {DefaultSleepInterval}");
                }

                if (portConfig.BaudRate < 1)
                {
                    portConfig.BaudRate = DefaultBaudRate;
                    Console.WriteLine($"Invalid baudRate for {key}. Set to default {DefaultBaudRate}");
                }

                if (string.IsNullOrEmpty(portConfig.Parity))
                {
                    portConfig.Parity = DefaultParity;
                    Console.WriteLine($"Missing parity for {key}. Set to default {DefaultParity}");

                    switch (portConfig.Parity)
                    {
                        case "None":
                            portConfig.ParityEnum = Parity.None;
                            break;
                        case "Even":
                            portConfig.ParityEnum = Parity.Even;
                            break;
                        case "Odd":
                            portConfig.ParityEnum = Parity.Odd;
                            break;
                        case "Mark":
                            portConfig.ParityEnum = Parity.Mark;
                            break;
                        case "Space":
                            portConfig.ParityEnum = Parity.Space;
                            break;
                    };

                }

                if (portConfig.DataBits < 1)
                {
                    portConfig.DataBits = DefaultDataBits;
                    Console.WriteLine($"Invalid databits for {key}. Set to default {DefaultDataBits}");
                }

                if (string.IsNullOrEmpty(portConfig.StopBits))
                {
                    portConfig.StopBits = DefaultStopBits;
                    Console.WriteLine($"Missing stopBits for {key}. Set to default {DefaultStopBits}");

                    switch (portConfig.StopBits)
                    {
                        case "None":
                            portConfig.StopBitsEnum = StopBits.None;
                            break;
                        case "One":
                            portConfig.StopBitsEnum = StopBits.One;
                            break;
                        case "OnePointFive":
                            portConfig.StopBitsEnum = StopBits.OnePointFive;
                            break;
                        case "Two":
                            portConfig.StopBitsEnum = StopBits.Two;
                            break;
                    };
                }

                if (string.IsNullOrEmpty(portConfig.Delimiter))
                {
                    portConfig.Delimiter = DefaultDelimiter;
                    Console.WriteLine($"Missing delimiter for {key}. Set to defailt {DefaultDelimiter}");
                }
            }

            foreach (var in_slave in invalidConfigs)
            {
                PortConfigs.Remove(in_slave);
            }
        }
    }
}