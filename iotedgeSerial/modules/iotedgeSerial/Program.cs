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
using Serilog;

namespace iotedgeSerial
{
    class Program
    {
        private const int SleepInterval = 10;
        private static ISerialDevice _serialPort = null;
        private static string _device = "/dev/ttyS0";
        private static int _baudRate = 9600;
        private static Parity _parity = Parity.None;
        private static int _dataBits = 8;
        private static StopBits _stopBits = StopBits.One;
        private static int _sleepInterval;
        private static string _delimiter = string.Empty;
        private static bool _ignoreEmptyLines = true;

        static void Main(string[] args)
        {
            InitLogging();
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
            
            //TODO: when publishing to Azure IoT Edge Modules Marketplace
            //ioTHubModuleClient.ProductInfo()
            
            await ioTHubModuleClient.OpenAsync();
            Log.Information($"IoT Hub module client initialized.");

            // Execute callback method for Twin desired properties updates
            var twin = await ioTHubModuleClient.GetTwinAsync();
            await onDesiredPropertiesUpdate(twin.Properties.Desired, ioTHubModuleClient);

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

            Log.Information($"Initializing module {Environment.GetEnvironmentVariable("IOTEDGE_MODULEID")}");
            Log.Information($".Net version in use: {Environment.GetEnvironmentVariable("DOTNET_VERSION")}");

            if (_device.Substring(0, 3) == "COM" || _device.Substring(0, 8) == "/dev/tty" || _device.Substring(0,11) == "/dev/rfcomm")
            {
                try
                {
                    Log.Information($"Opening '{_device}'...");

                    OpenSerial(_device, _baudRate, _parity, _dataBits, _stopBits);

                    //looping infinitely
                    while (true)
                    {
                        var response = ReadResponse();

                        var str = System.Text.Encoding.Default.GetString(response);

                        Log.Information($"Data read from {_device}: {str}");

                        var serialMessage = new SerialMessage
                        {
                            Data = str,
                            TimestampUtc = DateTime.UtcNow,
                            Device = _device
                        };

                        var jsonMessage = JsonConvert.SerializeObject(serialMessage);

                        Log.Information($"Message out: {jsonMessage}");

                        var pipeMessage = new Message(Encoding.UTF8.GetBytes(jsonMessage));
                        pipeMessage.Properties.Add("content-type", "application/edge-serial-json");

                        await client.SendEventAsync("serialOutput", pipeMessage);

                        Thread.Sleep(_sleepInterval);
                    }
                }
                catch (Exception ex)
                {
                    //clean up interrupted serial connection
                    Log.Error($"{DateTime.UtcNow} Exception: {ex.ToString()}");
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
            int bytesRead = 0;

            int delimiterIndex = 0;

            var temp = new List<byte>();

            var buf = new byte[1];

            //read until end delimiter is reached eg. \r\n in 12345\r\n67890
            while (bytesRead < 1024)
            {
                var i = _serialPort.Read(buf, 0, 1);

                if (i < 1)
                {
                    continue;
                }

                var str = System.Text.Encoding.Default.GetString(buf);

                temp.Add(buf[0]);

                if (str[0] != _delimiter[delimiterIndex])
                {
                    delimiterIndex = 0;    
                }
                else
                {
                    delimiterIndex++;
                    if (delimiterIndex == _delimiter.Length)
                    {
                        temp.RemoveRange(temp.Count-_delimiter.Length, _delimiter.Length);
                        break;
                    }
                }
                
                bytesRead++;
            }
            
            if (bytesRead == 1024)
            {
                Log.Warning("Delimiter not found in last 1024 bytes read.");
                temp.Clear();
            }

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
                Log.Information($"Desired property change: {JsonConvert.SerializeObject(desiredProperties)}");

                var client = userContext as ModuleClient;

                if (client == null)
                {
                    throw new InvalidOperationException("UserContext doesn't contain expected ModuleClient");
                }

                var reportedProperties = new TwinCollection();

                if (desiredProperties.Contains("sleepInterval"))
                {
                    if (desiredProperties["sleepInterval"] != null)
                    {
                        _sleepInterval = desiredProperties["sleepInterval"];
                    }
                    else
                    {
                        _sleepInterval = SleepInterval;
                    }

                    Log.Information($"Interval changed to: {_sleepInterval}");

                    reportedProperties["sleepInterval"] = _sleepInterval;
                }

                if (desiredProperties.Contains("device"))
                {
                    if (desiredProperties["device"] != null)
                    {
                        _device = desiredProperties["device"];
                    }
                    else
                    {
                        _device = "No device configured";
                    }

                    Log.Information($"Device changed to: {_device}");

                    reportedProperties["device"] = _device;
                }

                if (desiredProperties.Contains("baudRate"))
                {
                    if (desiredProperties["baudRate"] != null)
                    {
                        _baudRate = desiredProperties["baudRate"];
                    }
                    else
                    {
                        _baudRate = 9600;
                    }

                    Log.Information($"baud rate changed to {_baudRate}");

                    reportedProperties["baudRate"] = _baudRate;
                }

                if (desiredProperties.Contains("parity"))
                {
                    if (desiredProperties["parity"] != null)
                    {
                        switch (desiredProperties["parity"])
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

                    Log.Information($"Parity changed to: {_parity.ToString()}");

                    reportedProperties["parity"] = _parity.ToString();
                }

                if (desiredProperties.Contains("dataBits"))
                {
                    if (desiredProperties["dataBits"] != null)
                    {
                        _dataBits = desiredProperties["dataBits"];
                    }
                    else
                    {
                        _dataBits = 0;
                    }

                    Log.Information($"Data bits changed to: {_dataBits}");

                    reportedProperties["dataBits"] = _dataBits;
                }

                if (desiredProperties.Contains("stopBits"))
                {
                    if (desiredProperties["stopBits"] != null)
                    {
                        switch (desiredProperties["stopBits"])
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

                    Log.Information($"Stop bits changed to: {_stopBits.ToString()}");

                    reportedProperties["stopBits"] = _stopBits.ToString();
                }

                if (desiredProperties.Contains("delimiter"))
                {
                    if (desiredProperties["delimiter"] != null)
                    {
                        _delimiter = desiredProperties["delimiter"];
                    }
                    else
                    {
                        _delimiter = string.Empty;
                    }

                    Log.Information($"Delimiter changed to: {_delimiter}");

                    reportedProperties["delimiter"] = _delimiter;
                }

                if (desiredProperties.Contains("ignoreEmptyLines"))
                {
                    if (desiredProperties["ignoreEmptyLines"] != null)
                    {
                        _ignoreEmptyLines = desiredProperties["ignoreEmptyLines"];
                    }
                    else
                    {
                        _ignoreEmptyLines = true;
                    }

                    Log.Information($"Ignore empty lines changed to: {_ignoreEmptyLines}");

                    reportedProperties["ignoreEmptyLines"] = _ignoreEmptyLines;
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
                    Log.Error($"Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error when receiving desired property: {0}", ex.Message);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Initialize logging using Serilog
        /// LogLevel can be controlled via RuntimeLogLevel env var
        /// </summary>
        private static void InitLogging()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            var logLevel = Environment.GetEnvironmentVariable("RuntimeLogLevel");
            logLevel = !string.IsNullOrEmpty(logLevel) ? logLevel.ToLower() : "info";

            // set the log level
            switch (logLevel)
            {
                case "fatal":
                    loggerConfiguration.MinimumLevel.Fatal();
                    break;
                case "error":
                    loggerConfiguration.MinimumLevel.Error();
                    break;
                case "warn":
                    loggerConfiguration.MinimumLevel.Warning();
                    break;
                case "info":
                    loggerConfiguration.MinimumLevel.Information();
                    break;
                case "debug":
                    loggerConfiguration.MinimumLevel.Debug();
                    break;
                case "verbose":
                    loggerConfiguration.MinimumLevel.Verbose();
                    break;
            }

            // set logging sinks
            loggerConfiguration.WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] - {Message}{NewLine}{Exception}");
            loggerConfiguration.Enrich.FromLogContext();
            Log.Logger = loggerConfiguration.CreateLogger();
            Log.Information($"Initializied logger with log level {logLevel}");
        }
    }
}