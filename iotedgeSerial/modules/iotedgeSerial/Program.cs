using System;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System.IO.Ports;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using Serilog;

namespace iotedgeSerial
{
    class Program
    {
        static bool _run = true;

        private static int _maxBytesToRead = 1024;

        static List<Task> _taskList = new List<Task>();

        private static ModuleClient _ioTHubModuleClient = null;

        private static SerialMessageBroadcaster _serialMessageBroadcaster = new SerialMessageBroadcaster();

        /// <summary>
        /// Program Main() method to start it all
        /// </summary>
        static void Main(string[] args)
        {
            InitLogging();
            LogLogo();
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
                Log.Information($"IoT Hub module client initialized");

                // Execute callback method for Twin desired properties updates
                var twin = await _ioTHubModuleClient.GetTwinAsync();
                await OnDesiredPropertiesUpdate(twin.Properties.Desired, _ioTHubModuleClient);

                // Attach a callback for updates to the module twin's desired properties.
                await _ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, _ioTHubModuleClient);

                Log.Information($"Module '{Environment.GetEnvironmentVariable("IOTEDGE_MODULEID")}' initialized");
                Log.Information($".Net framework version '{Environment.GetEnvironmentVariable("DOTNET_VERSION")}' in use");
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Log.Error($"Error when initializing module: {exception}");
                }
            }
        }

        /// <summary>
        /// Call back function for updating the desired properties
        /// </summary>
        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            Log.Debug("OnDesiredPropertiesUpdate started");

            var ioTHubModuleClient = userContext as ModuleClient;

            if (desiredProperties == null)
            {
                Log.Debug("Empty desired properties ignored.");

                return;
            }

            try
            {
                try
                {
                    Log.Debug("Detaching handlers...");

                    // stop all activities while updating configuration
                    await ioTHubModuleClient.SetInputMessageHandlerAsync(
                        "serialInput",
                        DummyMessageCallBack,
                        null);

                    await ioTHubModuleClient.SetMethodHandlerAsync(
                        "serialWrite",
                        DummyMethodCallBack,
                        null);

                    Log.Debug("Detached handlers...");

                    _run = false;
                    await Task.WhenAll(_taskList); // wait until all tasks are completed

                    Log.Debug("Waited for all tasks to complete...");

                    _taskList.Clear();
                    _run = true;

                    Log.Debug("Task list cleared");

                    // start new activities with new set of desired properties
                    await SetupNewTasks(desiredProperties, ioTHubModuleClient);
                }
                finally
                {
                    Log.Debug("Attaching handlers...");

                    // assign input message handler again
                    await ioTHubModuleClient.SetInputMessageHandlerAsync(
                        "serialInput",
                        SerialMessageCallBack,
                        ioTHubModuleClient);

                    // assign direct method handler again
                    await ioTHubModuleClient.SetMethodHandlerAsync(
                        "serialWrite",
                        SerialWriteMethodCallBack,
                        ioTHubModuleClient);

                    Log.Debug("Attached handlers");
                }
            }
            catch (AggregateException ex)
            {
                Log.Error($"Desired properties change error: {ex}");
                
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Log.Error($"Error when receiving desired property: {exception}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error when receiving desired property: {ex.Message}");
            }
        }

        /// <summary>
        /// Dummy call back function to abandon messages during a desired properties update
        /// </summary>
        static async Task<MessageResponse> DummyMessageCallBack(Message message, object userContext)
        {
            Log.Debug("Executing DummyMessageCallBack");
            await Task.Delay(TimeSpan.FromSeconds(0));
            return MessageResponse.Abandoned;
        }

        /// <summary>
        /// Dummy call back function to abanon incoming direct method requests during a desired properties update 
        /// </summary>
        static async Task<MethodResponse> DummyMethodCallBack(MethodRequest methodRequest, object userContext)
        {
            Log.Debug("Executing DummyMethodCallBack");
            await Task.Delay(TimeSpan.FromSeconds(0));
            return new MethodResponse(501);
        }

        /// <summary>
        /// Call back function for handling incoming messages to be written to a serial port
        /// </summary>
        static async Task<MessageResponse> SerialMessageCallBack(Message message, object userContext)
        {
            Log.Debug("Executing SerialMessageCallBack");
            
            var messageBytes = message.GetBytes();
            var messageJson = Encoding.UTF8.GetString(messageBytes);
            var serialCommand = (SerialCommand)JsonConvert.DeserializeObject(messageJson, typeof(SerialCommand));
            
            _serialMessageBroadcaster.BroadcastMessage(serialCommand.Port, System.Text.Encoding.UTF8.GetBytes(serialCommand.Data)); 
            
            await Task.Delay(TimeSpan.FromSeconds(0));

            Log.Debug($"Serial command '{serialCommand.Data}' for serial port '{serialCommand.Port}' broadcasted");
            
            return MessageResponse.Completed;
        }

        /// <summary>
        /// Call back function for handling incoming direct method requests to be written to a serial port
        /// </summary>
        static async Task<MethodResponse> SerialWriteMethodCallBack(MethodRequest methodRequest, object userContext)
        {
            Log.Debug("Executing SerialWriteMethodCallBack");

            var messageBytes = methodRequest.Data;
            var messageJson = Encoding.UTF8.GetString(messageBytes);
            var serialCommand = (SerialCommand)JsonConvert.DeserializeObject(messageJson, typeof(SerialCommand));

            _serialMessageBroadcaster.BroadcastMessage(serialCommand.Port, System.Text.Encoding.UTF8.GetBytes(serialCommand.Data)); 

            await Task.Delay(TimeSpan.FromSeconds(0));

            Log.Debug($"Serial method '{serialCommand.Data}' for serial port '{serialCommand.Port}' broadcasted");

            return new MethodResponse(200);
        }


        /// <summary>
        /// Creating new task per port with updated desired properties
        /// </summary>
        private static async Task SetupNewTasks(TwinCollection desiredProperties, ModuleClient client)
        {
            Log.Debug("Changing desired properties");

            try
            {
                var serializedStr = JsonConvert.SerializeObject(desiredProperties);

                Log.Verbose($"Incoming desired properties change: '{serializedStr}'");

                ModuleConfig moduleConfig = JsonConvert.DeserializeObject<ModuleConfig>(serializedStr);

                moduleConfig.Validate();

                //// After setting all desired properties, we initialize and start 'read' and 'write' ports again

                Log.Debug("New desired twins are loaded into memory");

                foreach (var dict in moduleConfig.PortConfigs)
                {
                    var port = dict.Key;
                    var portConfig = dict.Value;

                    Log.Debug($"Adding task '{port}'");

                    var t = Task.Run(async () =>
                    {
                        await SerialTaskBody(port, portConfig, client, _serialMessageBroadcaster);
                    });

                    _taskList.Add(t);

                    Log.Debug($"Task '{port}' added ({_taskList.Count} tasks loaded)");
                }

                // report back received properties
                string reportedPropertiesJson = JsonConvert.SerializeObject(moduleConfig);

                Log.Verbose($"Reported properties: '{reportedPropertiesJson}'");

                var reportedProperties = new TwinCollection(reportedPropertiesJson);
                await client.UpdateReportedPropertiesAsync(reportedProperties);

                Log.Debug("Desired properties set");
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Log.Error($"Error when receiving desired property: '{exception.Message}'");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception when receiving desired property: '{ex.Message}'");
            }
        }

        /// <summary>
        /// Initialization of the serial port based on the port configuration
        /// </summary>
        private static ISerialDevice InitSerialPort(PortConfig portConfig)
        {
            if (portConfig.Device.Substring(0, 3) == "COM"
                    || portConfig.Device.Substring(0, 8) == "/dev/tty"
                    || portConfig.Device.Substring(0, 11) == "/dev/rfcomm")
            {
                try
                {
                    Log.Information($"Opening port '{portConfig.Device}' ({portConfig.BaudRate}, {portConfig.DataBits}, StopBitsEnum: {portConfig.StopBitsEnum}, ParityEnum: {portConfig.ParityEnum}) for '{portConfig.Direction}'");

                    var serialPort = OpenSerial(portConfig.Device,
                                                portConfig.BaudRate,
                                                portConfig.ParityEnum,
                                                portConfig.DataBits,
                                                portConfig.StopBitsEnum,
                                                portConfig.Direction);

                    return serialPort;
                }
                catch (Exception ex)
                {
                    Log.Error($"Exception: {ex.ToString()}");
                    Log.Debug($"Inner Exception: {ex.InnerException.ToString()}");
                }
            }

            return null;
        }

        /// <summary>
        /// Disposing a serial port in case of updating desired properties
        /// </summary>
        private static void DisposeSerialPort(ISerialDevice serialPort)
        {
            if (serialPort != null)
            {
                serialPort.Close();

                Log.Debug($"Serial port disposed");
            }

            Log.Debug("No serial port to dispose");
        }


        /// <summary>
        /// Execution method for a 'Read' or 'Write' task per port.
        /// </summary>
        private static async Task SerialTaskBody(string port, PortConfig portConfig, ModuleClient client, SerialMessageBroadcaster serialMessageBroadcaster)
        {
            Log.Debug($"Creating port");

            // create serial port
            var serialPort = InitSerialPort(portConfig);

            Log.Debug($"Port '{port}' created");

            if (portConfig.Direction == "Read")
            {
                Log.Debug($"Start read loop");

                // Looping infinitely until desired properties are updated.
                while (_run)
                {
                    var response = ReadResponse(serialPort, portConfig);

                    if (portConfig.IgnoreEmptyLines
                                    && response.Length == 0)
                    {
                        Log.Debug($"Ignore empty line");
                        continue;
                    }

                    var data = Encoding.UTF8.GetString(response);

                    Log.Debug($"Data read from '{portConfig.Device}': '{data}'");

                    var serialMessage = new SerialMessage
                    {
                        Data = data,
                        Port = port,
                        TimestampUtc = DateTime.UtcNow,
                    };

                    var jsonMessage = JsonConvert.SerializeObject(serialMessage);

                    Log.Debug($"Message out: '{jsonMessage}'");

                    var byteMessage = Encoding.UTF8.GetBytes(jsonMessage);
                    
                    using (var pipeMessage = new Message(byteMessage))
                    {
                        pipeMessage.Properties.Add("content-type", "application/edge-serial-json");

                        await client.SendEventAsync(port, pipeMessage);
                    }
                    
                    Log.Debug($"Message sent to output: {port}");

                    // wait a certain interval
                    await Task.Delay(portConfig.SleepInterval);
                }

                Log.Debug($"Disposing port '{port}'");

                // Ingest stopped. Tear down port
                DisposeSerialPort(serialPort);

                Log.Debug($"Disposed port '{port}'");
            }
            else if(portConfig.Direction == "Write")
            {
                Log.Debug("Let's write to serial");

                serialMessageBroadcaster.BroadcastEvent += (sender, se) =>
                { 
                    Log.Debug($"Executing BroadcastEvent for port '{se.Port}'");

                    if (se.Port == port)
                    {
                        Log.Debug($"BroadcastEvent has been picked up");

                        byte[] valueBytes = se.Message;
                        byte[] delimiterBytes = Encoding.UTF8.GetBytes(portConfig.Delimiter);
                        byte[] totalBytes = valueBytes.Concat(delimiterBytes).ToArray();

                        Log.Debug($"BroadcastEvent message converted");

                        if (totalBytes.Length > 0)
                        {
                            serialPort.Write(totalBytes, 0, totalBytes.Length);
                            Log.Information($"Written to '{se.Port}': '{ShowControlCharacters(Encoding.UTF8.GetString(totalBytes))}'");
                        }

                        Log.Debug($"BroadcastEvent message handled");
                    }
                };

                while (_run)
                {
                    await Task.Delay(portConfig.SleepInterval);
                };
            }
        }


        /// <summary>
        /// Open the initialized serial port
        /// </summary>
        private static ISerialDevice OpenSerial(string connection, int baudRate, Parity parity, int dataBits, StopBits stopBits, string direction)
        {
            ISerialDevice serialDevice = SerialDeviceFactory.CreateSerialDevice(connection, baudRate, parity, dataBits, stopBits);
            serialDevice.Open();

            Log.Information($"Serial port '{connection}' opened for '{direction}'");

            return serialDevice;
        }

        /// <summary>
        /// Read the byte[] response from a serial port
        /// </summary>
        private static byte[] ReadResponse(ISerialDevice serialPort, PortConfig portConfig)
        {
//            Log.Verbose("Read next line...");            

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

                if (str[0] != portConfig.Delimiter[delimiterIndex])
                {
                    delimiterIndex = 0;
                }
                else
                {
                    delimiterIndex++;
                    if (delimiterIndex == portConfig.Delimiter.Length)
                    {
                        temp.RemoveRange(temp.Count - portConfig.Delimiter.Length,
                                        portConfig.Delimiter.Length);
                        break;
                    }
                }

                bytesRead++;
            }

            if (bytesRead == _maxBytesToRead)
            {
                Log.Warning($"Delimiter '{ShowControlCharacters(portConfig.Delimiter)}' not found in last {_maxBytesToRead} bytes read.");
                temp.Clear();
            }

            if (!_run)
            {
                Log.Debug("Shutdown reading");
                temp.Clear();
            }

            return temp.ToArray();
        }

        /// <summary>
        /// Control characters are shown in plain text
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

            Log.Information($"Logger initialized with log level '{logLevel}'");
        }


        /// <summary>
        /// Log Module logo in ascii art.
        /// </summary>
        private static void LogLogo()
        {
            Log.Information("      _                         ___      _____   ___     _");
            Log.Information("     /_\\   ___ _  _  _ _  ___  |_ _| ___|_   _| | __| __| | __ _  ___  ");
            Log.Information("    / _ \\ |_ /| || || '_|/ -_)  | | / _ \\ | |   | _| / _` |/ _` |/ -_)");
            Log.Information("   /_/ \\_\\/__| \\_,_||_|  \\___| |___|\\___/ |_|   |___|\\__,_|\\__, |\\___|");
            Log.Information("                                                           |___/");
            Log.Information("      ___            _        _   __  __          _        _");
            Log.Information("     / __| ___  _ _ (_) __ _ | | |  \\/  | ___  __| | _  _ | | ___");
            Log.Information("     \\__ \\/ -_)| '_|| |/ _` || | | |\\/| |/ _ \\/ _` || || || |/ -_)");
            Log.Information("     |___/\\___||_|  |_|\\__,_||_| |_|  |_|\\___/\\__,_| \\_,_||_|\\___|");
            Log.Information(" ");
            Log.Information("   Copyright © 2019 - IoT Edge Foundation");
            Log.Information(" ");
        }
    }
}