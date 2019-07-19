using System;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Serilog;

namespace iotedgeSerial
{
    class Program
    {
        static bool _run = true;  
    
        static ModuleConfig _ModuleConfig = null;

        static List<Task> _task_list = new List<Task>(); 

        private static ModuleClient _ioTHubModuleClient = null;

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
            try
            {
                var setting = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
                ITransportSettings[] settings = { setting };

                // Open a connection to the Edge runtime
                _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

                //HOLD: when publishing to Azure IoT Edge Modules Marketplace
                //ioTHubModuleClient.ProductInfo = "...";

                await _ioTHubModuleClient.OpenAsync();
                Log.Information($"IoT Hub module client initialized.");
                Log.Information($"Initializing module {Environment.GetEnvironmentVariable("IOTEDGE_MODULEID")}");
                Log.Information($".Net version in use: {Environment.GetEnvironmentVariable("DOTNET_VERSION")}");

                // Execute callback method for Twin desired properties updates
                var twin = await _ioTHubModuleClient.GetTwinAsync();
                await OnDesiredPropertiesUpdate(twin.Properties.Desired, _ioTHubModuleClient);

                // Attach a callback for updates to the module twin's desired properties.
                await _ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, _ioTHubModuleClient);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when initializing module: {0}", exception);
                }
            }
        }

        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            Log.Information("[debug] OnDesiredPropertiesUpdate started");

            var ioTHubModuleClient = userContext as ModuleClient;

            try
            {
                // stop all activities while updating configuration
                await ioTHubModuleClient.SetInputMessageHandlerAsync(
                "serialInput",
                DummyCallBack,
                null);

                Log.Information("[debug] dummy attached");

                _run = false;
                await Task.WhenAll(_task_list); // wait until all tasks are completed

                Log.Information("[debug] Waited for all tasks to complete");

                _task_list.Clear();
                _run = true;

                Log.Information("[debug] list cleared");

                // start new activities with new set of desired properties
                await SetupNewTasks(desiredProperties, ioTHubModuleClient);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
        }

        static async Task<MessageResponse> DummyCallBack(Message message, object userContext)
        {
            await Task.Delay(TimeSpan.FromSeconds(0));
            return MessageResponse.Abandoned;
        }

        private static async Task SetupNewTasks(TwinCollection desiredProperties, ModuleClient client)
        {
            Log.Information("Changing desired properties");

            try
            {
                var  serializedStr = JsonConvert.SerializeObject(desiredProperties);

                Log.Information($"Desired property change: {serializedStr}");

                ModuleConfig moduleConfig = JsonConvert.DeserializeObject<ModuleConfig>(serializedStr);

                moduleConfig.Validate();

                _ModuleConfig = moduleConfig;
            
                //// After setting all desired properties, we initialize and start 'read' and 'write' ports again

                Log.Information("[debug] new desired twins are loaded into memory");

                foreach(var dict in _ModuleConfig.PortConfigs)
                {
                    var key = dict.Key;
                    var portConfig = dict.Value;

                    Log.Information($"[debug] adding task {key}");

                    var t = Task.Run(async () =>
                    {
                        await SerialTaskBody(key, portConfig, client);

                        // ik moet hier binnen de TASK een stukje code kunnen uitvoeren (schrijven naar poort)
                        // indien er buiten de task een input messagre arriveert
                        // graag alleen uitvoeren voor die ene poort
                    });

                    _task_list.Add(t);

                    Log.Information($"[debug] task {key} added ({_task_list.Count} tasks loaded)");
                }

                // report back received properties
                string reportedPropertiesJson = JsonConvert.SerializeObject(moduleConfig);
                var reportedProperties = new TwinCollection(reportedPropertiesJson);
                await client.UpdateReportedPropertiesAsync(reportedProperties);
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
        }

        private static ISerialDevice InitSerialPort(PortConfig portConfig)
        {            
            if (portConfig.device.Substring(0, 3) == "COM" 
                    || portConfig.device.Substring(0, 8) == "/dev/tty" 
                    || portConfig.device.Substring(0, 11) == "/dev/rfcomm")
            {
                try
                {
                    switch (portConfig.direction)
                    {
                        case "Read":
                            Log.Information($"Opening '{portConfig.device}' for reading...");
                            break;
                        case "Write":
                            Log.Information($"Opening '{portConfig.device}' for writing...");
                            break;
                    }

                    var serialPort = OpenSerial(portConfig.device, 
                                                portConfig.baudRate, 
                                                portConfig.Parity, 
                                                portConfig.dataBits, 
                                                portConfig.StopBits);

                    return serialPort;
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception: {ex.ToString()}");
                }
            }

            return null;
        }

        private static void DisposeSerialPort(ISerialDevice serialPort)
        {
            if (serialPort != null)
            {
                serialPort.Close();

                Log.Information($"Serial port disposed");
            }

            Log.Debug("No serial port to dispose");
        }

        private static async Task SerialTaskBody(string key, PortConfig portConfig, ModuleClient client)
        {
            Log.Information($"[debug] creating port");

            // create serial port
            var serialPort = InitSerialPort(portConfig);

            Log.Information($"[debug] port created");

            if (portConfig.direction == "Read")
            {
                Log.Information($"[debug] start read loop");

                //looping infinitely
                while (_run)
                {
                    var response = ReadResponse(serialPort, portConfig);

                    if (portConfig.ignoreEmptyLines
                                    && response.Length == 0)
                    {
                        Log.Information($"[debug] ignore empty line");
                        continue;
                    }

                    var str = System.Text.Encoding.Default.GetString(response);

                    Log.Information($"Data read from '{portConfig.device}': '{str}'");

                    var serialMessage = new SerialMessage
                    {
                        Data = str,
                        TimestampUtc = DateTime.UtcNow,
                        Device = portConfig.device
                    };

                    var jsonMessage = JsonConvert.SerializeObject(serialMessage);

                    Log.Information($"Message out: '{jsonMessage}'");

                    var pipeMessage = new Message(Encoding.UTF8.GetBytes(jsonMessage));
                    pipeMessage.Properties.Add("content-type", "application/edge-serial-json");

                    await client.SendEventAsync(key, pipeMessage);

                    Log.Information($"Message sent");

                    // wait a certain interval
                    await Task.Delay(portConfig.sleepInterval);
                }

                Log.Information($"[debug] disposing port {key}");

                // Ingest stopped. Tear down port
                DisposeSerialPort(serialPort);

                Log.Information($"[debug] disposed port {key}");
            }
        }

        private static ISerialDevice OpenSerial(string connection, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            ISerialDevice serialDevice = SerialDeviceFactory.CreateSerialDevice(connection, baudRate, parity, dataBits, stopBits);
            serialDevice.Open();
            Log.Information($"Serial port '{connection}' opened");

            return serialDevice;            
        }

        private static byte[] ReadResponse(ISerialDevice serialPort, PortConfig portConfig)
        {
            int bytesRead = 0;

            int delimiterIndex = 0;

            var temp = new List<byte>();

            var buf = new byte[1];

            //read until end delimiter is reached eg. \r\n in 12345\r\n67890
            while (_run && bytesRead < 1024)
            {
                var i = serialPort.Read(buf, 0, 1);

                if (i < 1)
                {
                    continue;
                }

                var str = System.Text.Encoding.Default.GetString(buf);

                temp.Add(buf[0]);

                if (str[0] != portConfig.delimiter[delimiterIndex])
                {
                    delimiterIndex = 0;
                }
                else
                {
                    delimiterIndex++;
                    if (delimiterIndex == portConfig.delimiter.Length)
                    {
                        temp.RemoveRange(temp.Count - portConfig.delimiter.Length,
                                        portConfig.delimiter.Length);
                        break;
                    }
                }

                bytesRead++;
            }

            if (bytesRead == 1024)
            {
                Log.Warning($"Delimiter '{ShowControlCharacters(portConfig.delimiter)}' not found in last 1024 bytes read.");
                temp.Clear();
            }

            if (!_run)
            {
                Log.Warning("Shutdown reading");
                temp.Clear();
            }

            Log.Warning("ready to show");

            return temp.ToArray();
        }

        /// <summary>
        /// comtrol characters are shown in plain text
        /// </summary>
        private static string ShowControlCharacters(string characters)
        {
            var result = string.Empty;

            foreach (char character in characters)
            {
                int characterCode = -1;
                if (Char.IsControl(character))
                {
                    characterCode = (int)character;
                    result += "\\0x" + characterCode;
                }
                else
                {
                    result += character;
                }
            }
            return result;
        }

        /// <summary>
        /// Initialize logging using Serilog
        /// LogLevel can be controlled via RuntimeLogLevel env var
        /// </summary>
        private static void InitLogging()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration();

            var logLevel = Environment.GetEnvironmentVariable("RuntimeLogLevel");
            logLevel = !string.IsNullOrEmpty(logLevel) ? logLevel.ToLower() : "verbose";

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