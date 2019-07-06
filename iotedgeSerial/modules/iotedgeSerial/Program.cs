using System;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;

using System.IO.Ports;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace iotedgeSerial
{
    class Program
    {
        private const int SleepInterval = 10;
        private static ISerialDevice _serialPort = null;
        private static string _device = "/dev/ttyxxx";
        private static int _baudRate = 9600;
        private static Parity _parity = Parity.None;
        private static int _dataBits = 8;
        private static StopBits _stopBits = StopBits.One;
        private static int _sleepInterval;

        private static string _beginDelimiter = "";
        private static string _endDelimiter = "";

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// serial port data
        /// </summary>
        static async Task Init()
        {

            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine($"[INF][{DateTime.UtcNow}] IoT Hub module client initialized.");

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await onDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

            // Register callback to be called when a message is received by the module
            //await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);
            var thread = new Thread(() => ThreadBody(ioTHubModuleClient));
            thread.Start();
        }

        private static async void ThreadBody(object userContext)
        {
            var client = userContext as ModuleClient;

            if (client == null)
            {
                throw new InvalidOperationException($"[INF][{DateTime.UtcNow}] UserContext for sending message doesn't contain expected values");
            }

            Console.WriteLine($"[INF] {DateTime.UtcNow} Initializing serial port");

            if (_device.Substring(0, 3) == "COM" || _device.Substring(0, 8) == "/dev/tty")
            {
                try
                {
                    Console.WriteLine($"[INF] {DateTime.UtcNow} Opening...'{_device}'");

                    OpenSerial(_device, _baudRate, _parity, _dataBits, _stopBits);

                    //looping infinitely
                    while (true)
                    {
                        var response = ReadResponse();

                        var str = System.Text.Encoding.Default.GetString(response);

                        Console.WriteLine($"[INF] {DateTime.UtcNow} Data read from serial port: {str}");

                        var serialMessage = new SerialPortMessage
                        {
                            Value = str,
                            PublishedUtcTimestamp = DateTime.UtcNow,
                            Port = _device
                        };

                        var jsonMessage = JsonConvert.SerializeObject(serialMessage);

                        Console.WriteLine($"[INF] {DateTime.UtcNow} Message to be sent: {jsonMessage}");

                        var pipeMessage = new Message(Encoding.UTF8.GetBytes(jsonMessage));
                        pipeMessage.Properties.Add("content-type", "application/edge-serial-json");

                        await client.SendEventAsync("serialOutput", pipeMessage);

                        Console.WriteLine($"[INF] {DateTime.UtcNow} Message sent.");

                        Thread.Sleep(_sleepInterval);
                    }
                }
                catch (Exception e)
                {
                    //clean up interrupted serial connection
                    Console.WriteLine($"[ERR] {DateTime.UtcNow} Exception: {e.ToString()}");
                    _serialPort = null;
                }
            }
        }

        private static void OpenSerial(string slaveConnection, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _serialPort = SerialDeviceFactory.CreateSerialDevice(slaveConnection, baudRate, parity, dataBits, stopBits);


            _serialPort.Open();
        }

        private static byte[] ReadResponse()
        {
            var temp = new List<byte>();

            var buf = new byte[1];

            var i = _serialPort.Read(buf, 0, 1);

            var str = System.Text.Encoding.Default.GetString(buf);

            //read until end delimiter is reached.
            while (str != _beginDelimiter)
            {
                temp.Add(buf[0]);

                i = _serialPort.Read(buf, 0, 1);

                str = System.Text.Encoding.Default.GetString(buf);
            }

            //remove the begin delimiter
            if (temp[0].ToString().Equals(_endDelimiter))
            {
                temp.Remove(temp[0]);
            }
            //TODO: always return the values no matter they are malformed.
            return temp.ToArray();
        }

        private static Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            if (desiredProperties.Count == 0)
            {
                return Task.CompletedTask;
            }

            try
            {
                Console.WriteLine($"[INF][{DateTime.UtcNow}] Desired property change:");
                Console.WriteLine($"[INF][{DateTime.UtcNow}] {JsonConvert.SerializeObject(desiredProperties)}");

                var client = userContext as ModuleClient;

                if (client == null)
                {
                    throw new InvalidOperationException($"[ERR][{DateTime.UtcNow}]UserContext doesn't contain expected ModuleClient");
                }

                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("SleepInterval"))
                {
                    if (desiredProperties["SleepInterval"] != null)
                    {
                        _sleepInterval = desiredProperties["SleepInterval"];
                    }
                    else
                    {
                        _sleepInterval = SleepInterval;
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}]Interval changed to {_sleepInterval}");

                    reportedProperties["SleepInterval"] = _sleepInterval;
                }

                if (desiredProperties.Contains("Device"))
                {
                    if (desiredProperties["Device"] != null)
                    {
                        _device = desiredProperties["Device"];
                    }
                    else
                    {
                        _device = "No device configured";
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}] Device changed to {_device}");

                    reportedProperties["Device"] = _device;
                }

                if (desiredProperties.Contains("BaudRate"))
                {
                    if (desiredProperties["BaudRate"] != null)
                    {
                        _baudRate = desiredProperties["BaudRate"];
                    }
                    else
                    {
                        _device = "No baudrate configured";
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}] baud rate changed to {_baudRate}");

                    reportedProperties["BaudRate"] = _baudRate;
                }

                if (desiredProperties.Contains("Parity"))
                {
                    if (desiredProperties["Parity"] != null)
                    {
                        switch(desiredProperties["Parity"])
                        {
                            case "None":
                                _parity = Parity.None;
                                break;
                            case "Even":
                                _parity = Parity.Even;
                                break;
                            case "Odd":
                                _parity = Parity.Odd;
                                break;
                            case "Mark":
                                _parity = Parity.Mark;
                                break;
                            case "Space":
                                _parity = Parity.Space;
                                break;

                        };
                    }
                    else
                    {
                        _parity = Parity.None;
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}] Parity changed to {_parity.ToString()}");

                    reportedProperties["Parity"] = _parity.ToString();
                }

                if (desiredProperties.Contains("DataBits"))
                {
                    if (desiredProperties["DataBits"] != null)
                    {
                        _dataBits = desiredProperties["DataBits"];
                    }
                    else
                    {
                        _dataBits = 0;
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}] Data bits changed to {_dataBits}");

                    reportedProperties["DataBits"] = _dataBits;
                }

                if (desiredProperties.Contains("StopBits"))
                {
                    if (desiredProperties["StopBits"] != null)
                    {
                        switch(desiredProperties["StopBits"])
                        {
                            case "None":
                                _stopBits = StopBits.None;
                                break;
                            case "One":
                                _stopBits = StopBits.One;
                                break;
                            case "OnePointFive":
                                _stopBits = StopBits.OnePointFive;
                                break;
                            case "Two":
                                _stopBits = StopBits.Two;
                                break;
                        };
                    }
                    else
                    {
                        _stopBits = StopBits.None;
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}] Stop bits changed to {_stopBits.ToString()}");

                    reportedProperties["StopBits"] = _stopBits.ToString();
                }

                if (desiredProperties.Contains("BeginDelimiter"))
                {
                    if (desiredProperties["BeginDelimiter"] != null)
                    {
                        _beginDelimiter = desiredProperties["BeginDelimiter"];
                    }
                    else
                    {
                        _beginDelimiter = "";
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}] Begin delimiter changed to {_beginDelimiter}");

                    reportedProperties["BeginDelimiter"] = _beginDelimiter;
                }

                if (desiredProperties.Contains("EndDelimiter"))
                {
                    if (desiredProperties["EndDelimiter"] != null)
                    {
                        _endDelimiter = desiredProperties["EndDelimiter"];
                    }
                    else
                    {
                        _endDelimiter = "";
                    }

                    Console.WriteLine($"[INF][{DateTime.UtcNow}] End delimiter changed to {_endDelimiter}");

                    reportedProperties["BeginDelimiter"] = _endDelimiter;
                }

                if (reportedProperties.Count > 0)
                {
                    client.UpdateReportedPropertiesAsync(reportedProperties).ConfigureAwait(false);
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine($"[INF][{DateTime.UtcNow}] Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"[INF][{DateTime.UtcNow}] Error when receiving desired property: {0}", ex.Message);
            }

            return Task.CompletedTask;
        }

    }
}
