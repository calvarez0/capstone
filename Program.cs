using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace ModbusActuatorControl
{
    class Program
    {
        private static ModbusMaster _master;
        private static List<ActuatorDevice> _devices = new List<ActuatorDevice>();
        private static SystemConfig _currentConfig;
        private static bool _isSimulationMode = false;

        static void Main(string[] args)
        {
            Console.WriteLine("=== Modbus RTU Actuator Control System ===");

            // Display runtime framework version
            string frameworkVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            Console.WriteLine($"Runtime: {frameworkVersion}\n");

            try
            {
                SelectOperationMode();
                RunMainMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nFatal error: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        static void SelectOperationMode()
        {
            Console.WriteLine("Select Operation Mode:");
            Console.WriteLine("1. Hardware Mode (Connect to real devices via COM port)");
            Console.WriteLine("2. Simulation Mode (Test without hardware)");
            Console.Write("\nSelect mode: ");

            var choice = Console.ReadLine();
            _isSimulationMode = choice == "2";

            if (_isSimulationMode)
            {
                Console.WriteLine("\n*** SIMULATION MODE ACTIVE ***");
                Console.WriteLine("You can test all features without connected hardware.\n");
                CreateSimulatedDevices();
            }
            else
            {
                Console.WriteLine("\n*** HARDWARE MODE ACTIVE ***");
                Console.WriteLine("You will connect to real devices via COM port.\n");
            }
        }

        static void RunMainMenu()
        {
            while (true)
            {
                Console.WriteLine($"\n--- Main Menu ({(_isSimulationMode ? "SIMULATION" : "HARDWARE")} MODE) ---");
                if (_isSimulationMode)
                {
                    Console.WriteLine("1. Create Simulated Devices");
                    Console.WriteLine("2. List Simulated Devices");
                }
                else
                {
                    Console.WriteLine("1. Connect to COM Port");
                    Console.WriteLine("2. Scan for Devices");
                }
                Console.WriteLine("3. Monitor Device Status");
                Console.WriteLine("4. Send Commands");
                Console.WriteLine("5. Configuration Management");
                Console.WriteLine("6. Calibration");
                Console.WriteLine("7. " + (_isSimulationMode ? "Clear Simulated Devices" : "Disconnect"));
                Console.WriteLine("8. Switch Mode");
                Console.WriteLine("0. Exit");
                Console.Write("\nSelect option: ");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        if (_isSimulationMode)
                            CreateSimulatedDevices();
                        else
                            ConnectToPort();
                        break;
                    case "2":
                        if (_isSimulationMode)
                            ListSimulatedDevices();
                        else
                            ScanDevices();
                        break;
                    case "3":
                        MonitorDevices();
                        break;
                    case "4":
                        SendCommandsMenu();
                        break;
                    case "5":
                        ConfigurationMenu();
                        break;
                    case "6":
                        CalibrationMenu();
                        break;
                    case "7":
                        if (_isSimulationMode)
                            ClearSimulatedDevices();
                        else
                            Disconnect();
                        break;
                    case "8":
                        SwitchMode();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
            }
        }

        static void CreateSimulatedDevices()
        {
            if (_master != null && _master.IsConnected)
            {
                Console.WriteLine("\nError: Simulated devices already created. Clear them first.");
                return;
            }

            Console.Write("\nEnter baud rate (default 9600): ");
            var baudInput = Console.ReadLine();
            int baudRate = string.IsNullOrWhiteSpace(baudInput) ? 9600 : int.Parse(baudInput);

            Console.Write("Enter parity (0=None, 1=Even, 2=Odd, default 0): ");
            var parityInput = Console.ReadLine();
            int parity = string.IsNullOrWhiteSpace(parityInput) ? 0 : int.Parse(parityInput);

            Console.Write("Enter stop bits (1 or 2, default 1): ");
            var stopBitsInput = Console.ReadLine();
            int stopBits = string.IsNullOrWhiteSpace(stopBitsInput) ? 1 : int.Parse(stopBitsInput);

            Console.Write("Enter starting slave ID (default 1): ");
            var startInput = Console.ReadLine();
            byte startId = string.IsNullOrWhiteSpace(startInput) ? (byte)1 : byte.Parse(startInput);

            Console.Write("Enter number of devices to create (default 1): ");
            var countInput = Console.ReadLine();
            int count = string.IsNullOrWhiteSpace(countInput) ? 1 : int.Parse(countInput);

            Console.WriteLine("\n--- Product Type ---");
            Console.WriteLine("1. S7X (0x8000)");
            Console.WriteLine("2. EHO (0x8001)");
            Console.WriteLine("3. Nova (0x8002)");
            Console.Write("\nSelect product type (default 1): ");
            var productInput = Console.ReadLine();
            int productChoice = string.IsNullOrWhiteSpace(productInput) ? 1 : int.Parse(productInput);

            ushort productIdentifier = productChoice switch
            {
                2 => 0x8001, // EHO
                3 => 0x8002, // Nova
                _ => 0x8000  // S7X (default)
            };

            string productName = productIdentifier switch
            {
                0x8000 => "S7X",
                0x8001 => "EHO",
                0x8002 => "Nova",
                _ => "Unknown"
            };

            // Create simulator master and connect
            _master = new ModbusMaster("SIM", baudRate, parity, stopBits, isSimulation: true);
            _master.Connect();

            // Add simulated slave devices
            for (byte i = 0; i < count; i++)
            {
                byte slaveId = (byte)(startId + i);
                _master.AddSlave(slaveId, (ushort)(i * 100), productIdentifier);
            }

            // Create ActuatorDevice instances for each slave
            _devices.Clear();
            var slaveIds = _master.GetSlaveIds();
            foreach (var slaveId in slaveIds)
            {
                _devices.Add(new ActuatorDevice(_master, slaveId));
            }

            if (_currentConfig == null)
            {
                _currentConfig = new SystemConfig();
            }
            _currentConfig.ComPort = "SIM";
            _currentConfig.BaudRate = baudRate;
            _currentConfig.Parity = parity;
            _currentConfig.StopBits = stopBits;

            Console.WriteLine($"\nCreated {count} simulated device(s).");
            Console.WriteLine($"Product Type: {productName} (0x{productIdentifier:X4})");
            Console.WriteLine($"Connection Settings: {baudRate} baud, Parity={parity}, StopBits={stopBits}");
            Console.WriteLine($"Found {_devices.Count} device(s).");

            // Automatically read configuration from the simulated devices
            Console.WriteLine("\nReading configuration from simulated devices...");
            _currentConfig.Actuators.Clear();
            foreach (var device in _devices)
            {
                try
                {
                    var config = device.ReadConfiguration();
                    _currentConfig.Actuators.Add(config);
                    Console.WriteLine($"  Read configuration from device {device.SlaveId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error: Failed to read from device {device.SlaveId}: {ex.Message}");
                }
            }
            Console.WriteLine($"Configuration loaded from {_currentConfig.Actuators.Count} device(s).");
        }

        static void ListSimulatedDevices()
        {
            if (_devices.Count == 0)
            {
                Console.WriteLine("\nError: No simulated devices found. Create some first.");
                return;
            }

            Console.WriteLine("\n--- Simulated Devices ---");
            foreach (var device in _devices)
            {
                device.UpdateStatus();
                Console.WriteLine($"  Slave ID {device.SlaveId}: Position={device.CurrentStatus.Position}");
            }
        }

        static void ClearSimulatedDevices()
        {
            foreach (var device in _devices)
            {
                device.StopPolling();
            }
            _devices.Clear();

            if (_master != null)
            {
                _master.Disconnect();
                _master = null;
            }

            Console.WriteLine("\nAll simulated devices cleared.");
        }

        static void SwitchMode()
        {
            Console.WriteLine("\nWarning: Switching mode will clear all current devices.");
            Console.Write("Continue? (y/n): ");
            if (Console.ReadLine()?.ToLower() == "y")
            {
                if (_isSimulationMode)
                {
                    ClearSimulatedDevices();
                }
                else
                {
                    Disconnect();
                }

                _isSimulationMode = !_isSimulationMode;
                Console.WriteLine($"Switched to {(_isSimulationMode ? "SIMULATION" : "HARDWARE")} MODE.");
            }
        }

        static void ConnectToPort()
        {
            if (_master != null && _master.IsConnected)
            {
                Console.WriteLine("\nError: Already connected. Disconnect first.");
                return;
            }

            Console.WriteLine("\n--- Available COM Ports ---");
            var ports = SerialPort.GetPortNames();
            foreach (var port in ports)
            {
                Console.WriteLine($"  {port}");
            }

            Console.Write("\nEnter COM port (e.g., COM3): ");
            var portName = Console.ReadLine();

            Console.Write("Enter baud rate (default 9600): ");
            var baudInput = Console.ReadLine();
            int baudRate = string.IsNullOrWhiteSpace(baudInput) ? 9600 : int.Parse(baudInput);

            Console.Write("Enter parity (0=None, 1=Even, 2=Odd, default 0): ");
            var parityInput = Console.ReadLine();
            int parityValue = string.IsNullOrWhiteSpace(parityInput) ? 0 : int.Parse(parityInput);
            Parity parity = parityValue switch
            {
                1 => Parity.Even,
                2 => Parity.Odd,
                _ => Parity.None
            };

            Console.Write("Enter stop bits (1 or 2, default 1): ");
            var stopBitsInput = Console.ReadLine();
            int stopBitsValue = string.IsNullOrWhiteSpace(stopBitsInput) ? 1 : int.Parse(stopBitsInput);
            StopBits stopBits = stopBitsValue == 2 ? StopBits.Two : StopBits.One;

            try
            {
                _master = new ModbusMaster(portName, baudRate, 8, parity, stopBits);
                _master.Connect();
                Console.WriteLine($"\nConnected successfully.");
                Console.WriteLine($"Settings: {baudRate} baud, Parity={parity}, StopBits={stopBits}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: Connection failed - {ex.Message}");
            }
        }

        static void ScanDevices()
        {
            if (!CheckConnection()) return;

            Console.Write("\nEnter starting slave ID (default 1): ");
            var startInput = Console.ReadLine();
            byte startId = string.IsNullOrWhiteSpace(startInput) ? (byte)1 : byte.Parse(startInput);

            Console.Write("Enter ending slave ID (default 10): ");
            var endInput = Console.ReadLine();
            byte endId = string.IsNullOrWhiteSpace(endInput) ? (byte)10 : byte.Parse(endInput);

            Console.WriteLine($"\nScanning slave IDs {startId} to {endId}...");
            _devices.Clear();

            for (byte id = startId; id <= endId; id++)
            {
                try
                {
                    var testDevice = new ActuatorDevice(_master, id);
                    testDevice.UpdateStatus();
                    _devices.Add(testDevice);
                    Console.WriteLine($"  Device found at slave ID {id}");
                }
                catch
                {
                }
            }

            Console.WriteLine($"Found {_devices.Count} device(s).");
        }

        static void MonitorDevices()
        {
            if (!CheckConnection()) return;

            if (_devices.Count == 0)
            {
                Console.WriteLine($"\nError: No devices found. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
                return;
            }

            Console.Write($"\nEnter polling interval in ms (default 1000): ");
            var intervalInput = Console.ReadLine();
            int interval = string.IsNullOrWhiteSpace(intervalInput) ? 1000 : int.Parse(intervalInput);

            foreach (var device in _devices)
            {
                device.StatusUpdated += Device_StatusUpdated;
                device.StartPolling(interval);
            }

            Console.WriteLine($"\nMonitoring {(_isSimulationMode ? "simulated " : "")}devices. Press any key to stop...");
            Console.ReadKey(true);

            foreach (var device in _devices)
            {
                device.StopPolling();
                device.StatusUpdated -= Device_StatusUpdated;
            }
        }

        static void Device_StatusUpdated(object sender, ActuatorStatus status)
        {
            var device = sender as ActuatorDevice;
            Console.WriteLine($"[{(_isSimulationMode ? "Simulated " : "")}Device {device.SlaveId}] {status}");
        }

        static void SendCommandsMenu()
        {
            if (!CheckConnection()) return;

            if (_devices.Count == 0)
            {
                Console.WriteLine($"\nError: No devices available. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
                return;
            }

            Console.WriteLine($"\n--- Send Commands {(_isSimulationMode ? "(Simulation)" : "")} ---");
            Console.Write("Enter slave ID: ");
            byte slaveId = byte.Parse(Console.ReadLine());

            var device = _devices.FirstOrDefault(d => d.SlaveId == slaveId);
            if (device == null)
            {
                Console.WriteLine($"\nError: Device {slaveId} not found.");
                return;
            }

            Console.WriteLine("\n--- Device Commands ---");
            Console.WriteLine("1. View Detailed Status");
            Console.WriteLine("2. Move to Position (0-4095)");
            Console.WriteLine("3. Open");
            Console.WriteLine("4. Close");
            Console.WriteLine("5. Stop");
            Console.WriteLine("6. ESD");
            Console.WriteLine("7. Toggle Setup Mode");
            Console.Write("\nSelect command: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        ViewDetailedStatus(device);
                        break;
                    case "2":
                        Console.Write("\nEnter target position (0-4095): ");
                        ushort position = ushort.Parse(Console.ReadLine());
                        device.MoveToPosition(position);
                        break;
                    case "3": // Open
                        HostCommandOpen(device);
                        break;
                    case "4": // Close
                        HostCommandClose(device);
                        break;
                    case "5": // Stop
                        HostCommandStop(device);
                        break;
                    case "6": // ESD
                        HostCommandESD(device);
                        break;
                    case "7": // Toggle Setup Mode
                        ToggleSetupMode(device);
                        break;
                    default:
                        Console.WriteLine("\nError: Invalid command.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: Command failed - {ex.Message}");
            }
        }

        static void HostCommandOpen(ActuatorDevice device)
        {
            try
            {
                device.MoveToPosition(4095); // Move to maximum position
                Console.WriteLine("\nOpening device to maximum position...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
        }

        static void HostCommandClose(ActuatorDevice device)
        {
            try
            {
                device.MoveToPosition(0); // Move to minimum position
                Console.WriteLine("\nClosing device to minimum position...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
        }

        static void HostCommandStop(ActuatorDevice device)
        {
            try
            {
                Console.Write("\nEnable stop mode? (y/n): ");
                bool enableStop = Console.ReadLine().ToLower() == "y";

                // Check if we're trying to disable stop mode while setup mode is on
                if (!enableStop)
                {
                    device.UpdateStatus();
                    if (device.CurrentStatus.Status.SetupMode)
                    {
                        Console.WriteLine("\nError: Cannot disable stop mode while setup mode is active. Exit setup mode first.");
                        return;
                    }
                }

                device.Stop(enableStop);
                Console.WriteLine(enableStop ? "\nStop mode enabled." : "\nStop mode disabled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
        }

        static void HostCommandESD(ActuatorDevice device)
        {
            try
            {
                var status = new DeviceStatus();
                status.HostEsdCmd = true;
                status.WriteCommandsToDevice(_master, device.SlaveId);
                Console.WriteLine("\nESD command sent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
        }

        static void ToggleSetupMode(ActuatorDevice device)
        {
            try
            {
                Console.Write("\nEnter setup mode? (y/n): ");
                bool enterSetup = Console.ReadLine().ToLower() == "y";

                // Update status to get current state
                device.UpdateStatus();

                // Check if stop mode is on before allowing setup mode entry
                if (enterSetup)
                {
                    if (!device.CurrentStatus.Status.StopMode)
                    {
                        Console.WriteLine("\nError: Cannot enter setup mode. Device must be in stop mode first (send stop command).");
                        return;
                    }
                }

                // Read current command state and only modify soft setup bit
                device.CurrentStatus.Status.SoftSetupCmd = enterSetup;
                device.CurrentStatus.Status.WriteCommandsToDevice(_master, device.SlaveId);
                Console.WriteLine(enterSetup ? "\nEntering setup mode..." : "\nExiting setup mode...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
        }

        static void ViewDetailedStatus(ActuatorDevice device)
        {
            try
            {
                device.UpdateStatus();
                Console.WriteLine("\n" + device.CurrentStatus.ToDetailedString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
        }

        static bool CheckConnection()
        {
            if (_master == null || !_master.IsConnected)
            {
                Console.WriteLine("\nError: Not connected. Connect first.");
                return false;
            }
            return true;
        }

        static void Disconnect()
        {
            foreach (var device in _devices)
            {
                device.StopPolling();
            }
            _devices.Clear();

            if (_master != null)
            {
                _master.Disconnect();
                _master = null;
            }

            Console.WriteLine("\nDisconnected.");
        }

        static void Cleanup()
        {
            if (_master != null && _master.IsConnected)
            {
                Disconnect();
            }
        }

        static void ConfigurationMenu()
        {
            Console.WriteLine("\n--- Configuration Management ---");
            Console.WriteLine("1. Load Configuration from File");
            Console.WriteLine("2. Save Configuration to File");
            Console.WriteLine("3. Apply Configuration to Devices");
            Console.WriteLine("4. Read Configuration from Devices");
            Console.WriteLine("5. Edit Device Configuration");
            Console.Write("\nSelect option: ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    LoadConfiguration();
                    break;
                case "2":
                    SaveConfiguration();
                    break;
                case "3":
                    ApplyConfiguration();
                    break;
                case "4":
                    ReadConfiguration();
                    break;
                case "5":
                    EditDeviceConfigurationMenu();
                    break;
                default:
                    Console.WriteLine("Invalid option");
                    break;
            }
        }

        static void LoadConfiguration()
        {
            Console.Write("\nEnter configuration file path: ");
            var filePath = Console.ReadLine();

            try
            {
                _currentConfig = SystemConfig.LoadFromFile(filePath);
                Console.WriteLine($"\nLoaded configuration with {_currentConfig.Actuators.Count} actuator(s).");

                // Validate product identifiers match connected devices
                if (_devices.Count > 0)
                {
                    Console.WriteLine("\nValidating product identifiers against connected devices...");
                    bool allValid = true;
                    foreach (var configActuator in _currentConfig.Actuators)
                    {
                        var device = _devices.FirstOrDefault(d => d.SlaveId == configActuator.SlaveId);
                        if (device != null && device.CurrentStatus != null)
                        {
                            ushort deviceProductId = device.CurrentStatus.ProductIdentifier;
                            if (deviceProductId != configActuator.ProductIdentifier)
                            {
                                string configProduct = ProductCapabilities.GetProductName(configActuator.ProductIdentifier);
                                string deviceProduct = ProductCapabilities.GetProductName(deviceProductId);
                                Console.WriteLine($"  Error: Device {configActuator.SlaveId} product mismatch!");
                                Console.WriteLine($"    Config expects: {configProduct} (0x{configActuator.ProductIdentifier:X4})");
                                Console.WriteLine($"    Device is: {deviceProduct} (0x{deviceProductId:X4})");
                                allValid = false;
                            }
                        }
                    }

                    if (!allValid)
                    {
                        Console.WriteLine("\nError: Configuration not loaded due to product identifier mismatch.");
                        Console.WriteLine("The config file is for different product types than the connected devices.");
                        _currentConfig = null;
                        return;
                    }
                    else
                    {
                        Console.WriteLine("  All product identifiers validated successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: Failed to load configuration - {ex.Message}");
            }
        }

        static void SaveConfiguration()
        {
            if (_currentConfig == null)
            {
                Console.WriteLine("\nError: No configuration to save. Read from devices first.");
                return;
            }

            Console.Write("\nEnter configuration file path: ");
            var filePath = Console.ReadLine();

            try
            {
                _currentConfig.SaveToFile(filePath);
                Console.WriteLine("\nConfiguration saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: Failed to save configuration - {ex.Message}");
            }
        }

        static void ApplyConfiguration()
        {
            if (!CheckConnection()) return;

            if (_currentConfig == null)
            {
                Console.WriteLine("\nError: No configuration loaded. Load from file first.");
                return;
            }

            Console.WriteLine("\nApplying configuration to devices...");
            foreach (var actuatorConfig in _currentConfig.Actuators)
            {
                var device = _devices.FirstOrDefault(d => d.SlaveId == actuatorConfig.SlaveId);
                if (device != null)
                {
                    try
                    {
                        device.ApplyConfiguration(actuatorConfig);
                        Console.WriteLine($"  Applied configuration to device {actuatorConfig.SlaveId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error: Failed to apply configuration to device {actuatorConfig.SlaveId} - {ex.Message}");
                    }
                }
            }
            Console.WriteLine("Configuration apply complete.");
        }

        static void ReadConfiguration()
        {
            if (!CheckConnection()) return;

            if (_devices.Count == 0)
            {
                Console.WriteLine($"\nError: No devices found. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
                return;
            }

            _currentConfig = new SystemConfig
            {
                ComPort = _isSimulationMode ? "SIM" : "COM1",
                BaudRate = 9600,
                Parity = 0,
                StopBits = 1,
                Actuators = new List<ActuatorConfig>()
            };

            Console.WriteLine("\nReading configuration from devices...");
            foreach (var device in _devices)
            {
                try
                {
                    var config = device.ReadConfiguration();
                    _currentConfig.Actuators.Add(config);
                    Console.WriteLine($"  Read configuration from device {device.SlaveId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error: Failed to read from device {device.SlaveId} - {ex.Message}");
                }
            }

            Console.WriteLine($"Configuration read from {_currentConfig.Actuators.Count} device(s).");
        }

        static void CalibrationMenu()
        {
            if (!CheckConnection()) return;

            if (_devices.Count == 0)
            {
                Console.WriteLine($"\nError: No devices available. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
                return;
            }

            Console.WriteLine("\n--- Calibration ---");
            Console.Write("Enter slave ID: ");
            byte slaveId = byte.Parse(Console.ReadLine());

            var device = _devices.FirstOrDefault(d => d.SlaveId == slaveId);
            if (device == null)
            {
                Console.WriteLine($"\nError: Device {slaveId} not found.");
                return;
            }

            try
            {
                // Update status to get current product identifier
                device.UpdateStatus();

                // Check if calibration registers (500-507) are available for this product
                if (!ProductCapabilities.IsRegisterAvailable(device.CurrentStatus.ProductIdentifier, 500))
                {
                    string productName = ProductCapabilities.GetProductName(device.CurrentStatus.ProductIdentifier);
                    Console.WriteLine($"\nError: Calibration is not available for {productName} devices.");
                    Console.WriteLine($"Product {productName} does not support calibration registers 500-507.");
                    return;
                }

                // Check if soft setup mode is on (reg 3 bit 9)
                if (!device.CurrentStatus.Status.SetupMode)
                {
                    Console.WriteLine("\nError: Cannot proceed with calibration. Device must be in setup mode first.");
                    Console.WriteLine("Use 'Send Commands > Toggle Setup Mode' to enable setup mode.");
                    return;
                }

                Console.WriteLine("\n=== Calibration Wizard ===");
                Console.WriteLine("You will set calibration values for analog inputs and outputs.");
                Console.WriteLine("Valid range: 0-4095 (representing 0.024% units)\n");

                // Get current calibration values
                var config = device.ReadConfiguration();

                // Analog Input 1
                Console.WriteLine("--- Analog Input 1 ---");
                Console.Write($"Enter Zero Calibration (0-4095, current={config.Config.AnalogInput1ZeroCalibration}): ");
                var input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogInput1ZeroCalibration = ParseCalibrationValue(input);

                Console.Write($"Enter Span Calibration (0-4095, current={config.Config.AnalogInput1SpanCalibration}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogInput1SpanCalibration = ParseCalibrationValue(input);

                // Analog Input 2
                Console.WriteLine("\n--- Analog Input 2 ---");
                Console.Write($"Enter Zero Calibration (0-4095, current={config.Config.AnalogInput2ZeroCalibration}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogInput2ZeroCalibration = ParseCalibrationValue(input);

                Console.Write($"Enter Span Calibration (0-4095, current={config.Config.AnalogInput2SpanCalibration}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogInput2SpanCalibration = ParseCalibrationValue(input);

                // Analog Output 1
                Console.WriteLine("\n--- Analog Output 1 ---");
                Console.Write($"Enter Zero Calibration (0-4095, current={config.Config.AnalogOutput1ZeroCalibration}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogOutput1ZeroCalibration = ParseCalibrationValue(input);

                Console.Write($"Enter Span Calibration (0-4095, current={config.Config.AnalogOutput1SpanCalibration}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogOutput1SpanCalibration = ParseCalibrationValue(input);

                // Analog Output 2
                Console.WriteLine("\n--- Analog Output 2 ---");
                Console.Write($"Enter Zero Calibration (0-4095, current={config.Config.AnalogOutput2ZeroCalibration}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogOutput2ZeroCalibration = ParseCalibrationValue(input);

                Console.Write($"Enter Span Calibration (0-4095, current={config.Config.AnalogOutput2SpanCalibration}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    config.Config.AnalogOutput2SpanCalibration = ParseCalibrationValue(input);

                // Display summary
                Console.WriteLine("\n=== Calibration Summary ===");
                Console.WriteLine($"AI1 Zero: {config.Config.AnalogInput1ZeroCalibration} ({config.Config.AnalogInput1ZeroCalibration * 0.024:F2}%)");
                Console.WriteLine($"AI1 Span: {config.Config.AnalogInput1SpanCalibration} ({config.Config.AnalogInput1SpanCalibration * 0.024:F2}%)");
                Console.WriteLine($"AI2 Zero: {config.Config.AnalogInput2ZeroCalibration} ({config.Config.AnalogInput2ZeroCalibration * 0.024:F2}%)");
                Console.WriteLine($"AI2 Span: {config.Config.AnalogInput2SpanCalibration} ({config.Config.AnalogInput2SpanCalibration * 0.024:F2}%)");
                Console.WriteLine($"AO1 Zero: {config.Config.AnalogOutput1ZeroCalibration} ({config.Config.AnalogOutput1ZeroCalibration * 0.024:F2}%)");
                Console.WriteLine($"AO1 Span: {config.Config.AnalogOutput1SpanCalibration} ({config.Config.AnalogOutput1SpanCalibration * 0.024:F2}%)");
                Console.WriteLine($"AO2 Zero: {config.Config.AnalogOutput2ZeroCalibration} ({config.Config.AnalogOutput2ZeroCalibration * 0.024:F2}%)");
                Console.WriteLine($"AO2 Span: {config.Config.AnalogOutput2SpanCalibration} ({config.Config.AnalogOutput2SpanCalibration * 0.024:F2}%)");

                Console.Write("\nApply these calibration values? (y/n): ");
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    // Write calibration values to device
                    config.Config.WriteCalibrationToDevice(_master, slaveId);
                    Console.WriteLine("\nCalibration values written to device.");

                    // Turn off soft setup mode
                    device.UpdateStatus();
                    device.CurrentStatus.Status.SoftSetupCmd = false;
                    device.CurrentStatus.Status.WriteCommandsToDevice(_master, slaveId);
                    Console.WriteLine("Exiting setup mode.");

                    // Update the config in memory if it exists
                    if (_currentConfig != null)
                    {
                        var actuatorConfig = _currentConfig.Actuators.FirstOrDefault(a => a.SlaveId == slaveId);
                        if (actuatorConfig != null)
                        {
                            actuatorConfig.Config = config.Config;
                            Console.WriteLine("Configuration updated in memory. Use 'Save Configuration' to persist changes.");
                        }
                    }

                    Console.WriteLine("\nCalibration complete.");
                }
                else
                {
                    Console.WriteLine("\nCalibration cancelled.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: Calibration failed - {ex.Message}");
            }
        }

        static ushort ParseCalibrationValue(string input)
        {
            if (ushort.TryParse(input, out ushort value))
            {
                if (value > 4095)
                {
                    Console.WriteLine($"Warning: Value {value} exceeds maximum (4095), using 4095.");
                    return 4095;
                }
                return value;
            }
            return 0;
        }

        static void EditDeviceConfigurationMenu()
        {
            if (_currentConfig == null || _currentConfig.Actuators.Count == 0)
            {
                Console.WriteLine("\nError: No configuration loaded. Load from file or read from devices first.");
                return;
            }

            Console.WriteLine("\n--- Select Device to Edit ---");
            for (int i = 0; i < _currentConfig.Actuators.Count; i++)
            {
                var actuator = _currentConfig.Actuators[i];
                Console.WriteLine($"{i + 1}. Slave ID {actuator.SlaveId} - {actuator.DeviceName}");
            }
            Console.Write("\nSelect device (number): ");

            if (int.TryParse(Console.ReadLine(), out int deviceIndex) && deviceIndex > 0 && deviceIndex <= _currentConfig.Actuators.Count)
            {
                EditDeviceConfiguration(_currentConfig.Actuators[deviceIndex - 1]);
            }
            else
            {
                Console.WriteLine("\nError: Invalid selection.");
            }
        }

        static void EditDeviceConfiguration(ActuatorConfig config)
        {
            while (true)
            {
                Console.WriteLine($"\n--- Edit Device {config.SlaveId} Configuration ---");
                string productName = ProductCapabilities.GetProductName(config.ProductIdentifier);
                Console.WriteLine($"Product Type: {productName}\n");

                // Build dynamic menu based on available registers
                var menuOptions = new Dictionary<string, (string description, Action action)>();
                int optionNum = 1;

                // Basic Settings (Register 112) - Torque
                if (ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 112))
                {
                    menuOptions[optionNum.ToString()] = ("Basic Settings (Torque)", () => EditBasicSettings(config));
                    optionNum++;
                }

                // Register 11 Flags - check if any bits are available
                if (ProductCapabilities.GetAvailableBits(config.ProductIdentifier, 11).Count > 0)
                {
                    menuOptions[optionNum.ToString()] = ("Register 11 Flags (EHO, Input Functions, Polarities, Triggers)", () => EditRegister11Flags(config.Config, config.ProductIdentifier));
                    optionNum++;
                }

                // Register 12 Flags - check if any bits are available
                if (ProductCapabilities.GetAvailableBits(config.ProductIdentifier, 12).Count > 0)
                {
                    menuOptions[optionNum.ToString()] = ("Register 12 Flags (Torque, Display, Inhibits, ESD, Speed)", () => EditRegister12Flags(config.Config, config.ProductIdentifier));
                    optionNum++;
                }

                // Control Configuration (Registers 101-102, 113-115)
                bool hasControlConfig = ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 101) ||
                                       ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 102) ||
                                       ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 113) ||
                                       ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 114) ||
                                       ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 115);
                if (hasControlConfig)
                {
                    menuOptions[optionNum.ToString()] = ("Control Configuration (Control Mode, Modulation, Deadband, LSA/LSB, Speed Control)", () => EditControlConfiguration(config.Config, config.ProductIdentifier));
                    optionNum++;
                }

                // Relay Configuration (Registers 103-106)
                bool hasRelayConfig = ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 103);
                if (hasRelayConfig)
                {
                    menuOptions[optionNum.ToString()] = ("Relay Configuration (9 Relays)", () => EditRelayConfiguration(config.Config));
                    optionNum++;
                }

                // Additional Functions (Registers 107-110)
                bool hasAdditionalFunctions = ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 107) ||
                                             ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 108) ||
                                             ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 109) ||
                                             ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 110);
                if (hasAdditionalFunctions)
                {
                    menuOptions[optionNum.ToString()] = ("Additional Functions (Failsafe, ESD, Loss Comm)", () => EditAdditionalFunctions(config.Config, config.ProductIdentifier));
                    optionNum++;
                }

                // Network Settings (Registers 110-111) - always available
                menuOptions[optionNum.ToString()] = ("Network Settings (Baud Rate, Parity, Response Delay)", () => EditNetworkSettings(config.Config));

                // Display menu
                foreach (var option in menuOptions)
                {
                    Console.WriteLine($"{option.Key}. {option.Value.description}");
                }
                Console.WriteLine("0. Back");
                Console.Write("\nSelect option: ");

                var choice = Console.ReadLine();

                if (choice == "0")
                    return;

                if (menuOptions.ContainsKey(choice))
                {
                    menuOptions[choice].action();
                }
                else
                {
                    Console.WriteLine("\nError: Invalid option.");
                }
            }
        }

        static void EditBasicSettings(ActuatorConfig config)
        {
            Console.WriteLine($"\n--- Basic Settings ---");
            Console.WriteLine($"1. Close Torque: {config.CloseTorque}%");
            Console.WriteLine($"2. Open Torque: {config.OpenTorque}%");
            Console.Write("\nEnter number to edit (0 to exit): ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= 2)
            {
                switch (choice)
                {
                    case 1:
                        Console.Write("Enter Close Torque (15-100): ");
                        if (ushort.TryParse(Console.ReadLine(), out ushort closeTorque))
                            config.CloseTorque = closeTorque;
                        break;
                    case 2:
                        Console.Write("Enter Open Torque (15-100): ");
                        if (ushort.TryParse(Console.ReadLine(), out ushort openTorque))
                            config.OpenTorque = openTorque;
                        break;
                }
                Console.WriteLine("\nUpdated.");
            }
        }

        static void EditRegister11Flags(DeviceConfig config, ushort productId)
        {
            Console.WriteLine($"\n--- Register 11 Flags ---");

            // Build dynamic menu based on available bits
            var options = new List<(int num, string desc, Action toggle, int bit)>();
            int num = 1;

            // Map each option to its bit in register 11 and check if available
            if (ProductCapabilities.IsBitAvailable(productId, 11, 0))
                options.Add((num++, $"EHO Type: {config.EhoType}", () => config.EhoType = config.EhoType == EhoType.DoubleAction ? EhoType.SpringReturn : EhoType.DoubleAction, 0));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 1))
                options.Add((num++, $"Local Input Function: {config.LocalInputFunction}", () => config.LocalInputFunction = config.LocalInputFunction == InputFunction.Maintained ? InputFunction.Momentary : InputFunction.Maintained, 1));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 2))
                options.Add((num++, $"Remote Input Function: {config.RemoteInputFunction}", () => config.RemoteInputFunction = config.RemoteInputFunction == InputFunction.Maintained ? InputFunction.Momentary : InputFunction.Maintained, 2));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 3))
                options.Add((num++, $"Remote ESD Enabled: {config.RemoteEsdEnabled}", () => config.RemoteEsdEnabled = config.RemoteEsdEnabled == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled, 3));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 4))
                options.Add((num++, $"Loss Comm Enabled: {config.LossCommEnabled}", () => config.LossCommEnabled = config.LossCommEnabled == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled, 4));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 5))
                options.Add((num++, $"AI1 Polarity: {config.Ai1Polarity}", () => config.Ai1Polarity = config.Ai1Polarity == Polarity.Normal ? Polarity.Reversed : Polarity.Normal, 5));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 6))
                options.Add((num++, $"AI2 Polarity: {config.Ai2Polarity}", () => config.Ai2Polarity = config.Ai2Polarity == Polarity.Normal ? Polarity.Reversed : Polarity.Normal, 6));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 7))
                options.Add((num++, $"AO1 Polarity: {config.Ao1Polarity}", () => config.Ao1Polarity = config.Ao1Polarity == Polarity.Normal ? Polarity.Reversed : Polarity.Normal, 7));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 8))
                options.Add((num++, $"AO2 Polarity: {config.Ao2Polarity}", () => config.Ao2Polarity = config.Ao2Polarity == Polarity.Normal ? Polarity.Reversed : Polarity.Normal, 8));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 9))
                options.Add((num++, $"DI1 Open Trigger: {config.Di1OpenTrigger}", () => config.Di1OpenTrigger = config.Di1OpenTrigger == TriggerType.NormallyOpen ? TriggerType.NormallyClose : TriggerType.NormallyOpen, 9));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 10))
                options.Add((num++, $"DI2 Close Trigger: {config.Di2CloseTrigger}", () => config.Di2CloseTrigger = config.Di2CloseTrigger == TriggerType.NormallyOpen ? TriggerType.NormallyClose : TriggerType.NormallyOpen, 10));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 11))
                options.Add((num++, $"DI3 Stop Trigger: {config.Di3StopTrigger}", () => config.Di3StopTrigger = config.Di3StopTrigger == TriggerType.NormallyOpen ? TriggerType.NormallyClose : TriggerType.NormallyOpen, 11));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 12))
                options.Add((num++, $"DI4 ESD Trigger: {config.Di4EsdTrigger}", () => config.Di4EsdTrigger = config.Di4EsdTrigger == TriggerType.NormallyOpen ? TriggerType.NormallyClose : TriggerType.NormallyOpen, 12));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 13))
                options.Add((num++, $"DI5 PST Trigger: {config.Di5PstTrigger}", () => config.Di5PstTrigger = config.Di5PstTrigger == TriggerType.NormallyOpen ? TriggerType.NormallyClose : TriggerType.NormallyOpen, 13));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 14))
                options.Add((num++, $"Close Direction: {config.CloseDirection}", () => config.CloseDirection = config.CloseDirection == CloseDirection.Clockwise ? CloseDirection.CounterClockwise : CloseDirection.Clockwise, 14));
            if (ProductCapabilities.IsBitAvailable(productId, 11, 15))
                options.Add((num++, $"Seat Mode: {config.Seat}", () => config.Seat = config.Seat == SeatMode.Position ? SeatMode.Torque : SeatMode.Position, 15));

            // Display options
            foreach (var opt in options)
            {
                Console.WriteLine($"{opt.num}. {opt.desc}");
            }
            Console.Write("\nEnter number to toggle (0 to exit): ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= options.Count)
            {
                options[choice - 1].toggle();
                Console.WriteLine("\nUpdated.");
            }
        }

        static void EditRegister12Flags(DeviceConfig config, ushort productId)
        {
            Console.WriteLine($"\n--- Register 12 Flags ---");
            Console.WriteLine($"1. Torque Backseat: {config.TorqueBackseat}");
            Console.WriteLine($"2. Torque Retry: {config.TorqueRetry}");
            Console.WriteLine($"3. Remote Display: {config.RemoteDisplay}");
            Console.WriteLine($"4. LEDs: {config.Leds}");
            Console.WriteLine($"5. Open Inhibit: {config.OpenInhibit}");
            Console.WriteLine($"6. Close Inhibit: {config.CloseInhibit}");
            Console.WriteLine($"7. Local ESD: {config.LocalEsd}");
            Console.WriteLine($"8. ESD Or Thermal: {config.EsdOrThermal}");
            Console.WriteLine($"9. ESD Or Local: {config.EsdOrLocal}");
            Console.WriteLine($"10. ESD Or Stop: {config.EsdOrStop}");
            Console.WriteLine($"11. ESD Or Inhibit: {config.EsdOrInhibit}");
            Console.WriteLine($"12. ESD Or Torque: {config.EsdOrTorque}");
            Console.WriteLine($"13. Close Speed Control: {config.CloseSpeedControl}");
            Console.WriteLine($"14. Open Speed Control: {config.OpenSpeedControl}");
            Console.Write("\nEnter number to toggle (0 to exit): ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= 14)
            {
                switch (choice)
                {
                    case 1: config.TorqueBackseat = config.TorqueBackseat == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 2: config.TorqueRetry = config.TorqueRetry == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 3: config.RemoteDisplay = config.RemoteDisplay == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 4: config.Leds = config.Leds == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 5: config.OpenInhibit = config.OpenInhibit == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 6: config.CloseInhibit = config.CloseInhibit == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 7: config.LocalEsd = config.LocalEsd == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 8: config.EsdOrThermal = config.EsdOrThermal == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 9: config.EsdOrLocal = config.EsdOrLocal == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 10: config.EsdOrStop = config.EsdOrStop == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 11: config.EsdOrInhibit = config.EsdOrInhibit == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 12: config.EsdOrTorque = config.EsdOrTorque == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 13: config.CloseSpeedControl = config.CloseSpeedControl == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                    case 14: config.OpenSpeedControl = config.OpenSpeedControl == EnabledState.Disabled ? EnabledState.Enabled : EnabledState.Disabled; break;
                }
                Console.WriteLine("\nUpdated.");
            }
        }

        static void EditControlConfiguration(DeviceConfig config, ushort productId)
        {
            Console.WriteLine($"\n--- Control Configuration ---");
            Console.WriteLine($"1. Control Mode: {config.ControlMode}");
            Console.WriteLine($"2. Modulation Delay: {config.ModulationDelay}");
            Console.WriteLine($"3. Deadband: {config.Deadband}");
            Console.WriteLine($"4. Network Adapter: {config.NetworkAdapter}");
            Console.WriteLine($"5. LSA: {config.LSA}%");
            Console.WriteLine($"6. LSB: {config.LSB}%");
            Console.WriteLine($"7. Open Speed Control Start: {config.OpenSpeedControlStart}%");
            Console.WriteLine($"8. Open Speed Control Ratio: {config.OpenSpeedControlRatio}%");
            Console.WriteLine($"9. Close Speed Control Start: {config.CloseSpeedControlStart}%");
            Console.WriteLine($"10. Close Speed Control Ratio: {config.CloseSpeedControlRatio}%");
            Console.Write("\nEnter number to edit (0 to exit): ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= 10)
            {
                switch (choice)
                {
                    case 1:
                        Console.Write("Enter Control Mode (0-8): ");
                        if (int.TryParse(Console.ReadLine(), out int controlMode))
                            config.ControlMode = (ControlMode)controlMode;
                        break;
                    case 2:
                        Console.Write("Enter Modulation Delay (0-255): ");
                        if (byte.TryParse(Console.ReadLine(), out byte modDelay))
                            config.ModulationDelay = modDelay;
                        break;
                    case 3:
                        Console.Write("Enter Deadband (0-255): ");
                        if (byte.TryParse(Console.ReadLine(), out byte deadband))
                            config.Deadband = deadband;
                        break;
                    case 4:
                        Console.Write("Enter Network Adapter (0-10): ");
                        if (int.TryParse(Console.ReadLine(), out int netAdapter))
                            config.NetworkAdapter = (NetworkAdapter)netAdapter;
                        break;
                    case 5:
                        Console.Write("Enter LSA (1-99%): ");
                        if (byte.TryParse(Console.ReadLine(), out byte lsa))
                        {
                            if (lsa < 1) lsa = 1;
                            if (lsa > 99) lsa = 99;
                            config.LSA = lsa;
                        }
                        break;
                    case 6:
                        Console.Write("Enter LSB (1-99%): ");
                        if (byte.TryParse(Console.ReadLine(), out byte lsb))
                        {
                            if (lsb < 1) lsb = 1;
                            if (lsb > 99) lsb = 99;
                            config.LSB = lsb;
                        }
                        break;
                    case 7:
                        Console.Write("Enter Open Speed Control Start (5-95%, multiple of 5): ");
                        if (byte.TryParse(Console.ReadLine(), out byte openStart))
                        {
                            if (openStart < 5) openStart = 5;
                            if (openStart > 95) openStart = 95;
                            int remainder = openStart % 5;
                            if (remainder != 0)
                                openStart = (byte)(remainder < 3 ? openStart - remainder : openStart + (5 - remainder));
                            config.OpenSpeedControlStart = openStart;
                        }
                        break;
                    case 8:
                        Console.Write("Enter Open Speed Control Ratio (5-95%, multiple of 5): ");
                        if (byte.TryParse(Console.ReadLine(), out byte openRatio))
                        {
                            if (openRatio < 5) openRatio = 5;
                            if (openRatio > 95) openRatio = 95;
                            int remainder = openRatio % 5;
                            if (remainder != 0)
                                openRatio = (byte)(remainder < 3 ? openRatio - remainder : openRatio + (5 - remainder));
                            config.OpenSpeedControlRatio = openRatio;
                        }
                        break;
                    case 9:
                        Console.Write("Enter Close Speed Control Start (5-95%, multiple of 5): ");
                        if (byte.TryParse(Console.ReadLine(), out byte closeStart))
                        {
                            if (closeStart < 5) closeStart = 5;
                            if (closeStart > 95) closeStart = 95;
                            int remainder = closeStart % 5;
                            if (remainder != 0)
                                closeStart = (byte)(remainder < 3 ? closeStart - remainder : closeStart + (5 - remainder));
                            config.CloseSpeedControlStart = closeStart;
                        }
                        break;
                    case 10:
                        Console.Write("Enter Close Speed Control Ratio (5-95%, multiple of 5): ");
                        if (byte.TryParse(Console.ReadLine(), out byte closeRatio))
                        {
                            if (closeRatio < 5) closeRatio = 5;
                            if (closeRatio > 95) closeRatio = 95;
                            int remainder = closeRatio % 5;
                            if (remainder != 0)
                                closeRatio = (byte)(remainder < 3 ? closeRatio - remainder : closeRatio + (5 - remainder));
                            config.CloseSpeedControlRatio = closeRatio;
                        }
                        break;
                }
                Console.WriteLine("\nUpdated.");
            }
        }

        static void EditRelayConfiguration(DeviceConfig config)
        {
            Console.WriteLine($"\n--- Relay Configuration ---");
            for (int i = 0; i < config.Relays.Count; i++)
            {
                var relay = config.Relays[i];
                Console.WriteLine($"{i + 1}. Relay {i + 1}: Trigger={relay.trigger}, Mode={relay.mode}, Contact={relay.contact}");
            }

            Console.Write("\nEnter relay number to edit (0 to exit): ");
            if (int.TryParse(Console.ReadLine(), out int relayNum) && relayNum > 0 && relayNum <= config.Relays.Count)
            {
                var relay = config.Relays[relayNum - 1];

                Console.Write($"\nEnter Trigger (0-29, current={relay.trigger}): ");
                var input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    relay.trigger = (RelayTrigger)int.Parse(input);

                Console.Write($"Enter Mode (0=Continuous, 1=Flashing, current={relay.mode}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    relay.mode = (RelayMode)int.Parse(input);

                Console.Write($"Enter Contact Type (0=NormallyClosed, 1=NormallyOpen, current={relay.contact}): ");
                input = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(input))
                    relay.contact = (RelayContactType)int.Parse(input);

                config.Relays[relayNum - 1] = relay;
                Console.WriteLine("\nRelay updated.");
            }
        }

        static void EditAdditionalFunctions(DeviceConfig config, ushort productId)
        {
            Console.WriteLine($"\n--- Additional Functions ---");
            Console.WriteLine($"1. Failsafe Function: {config.FailsafeFunction}");
            Console.WriteLine($"2. Failsafe Go To Position: {config.FailsafeGoToPosition}%");
            Console.WriteLine($"3. ESD Function: {config.EsdFunction}");
            Console.WriteLine($"4. ESD Delay: {config.EsdDelay}");
            Console.WriteLine($"5. Loss Comm Function: {config.LossCommFunction}");
            Console.WriteLine($"6. Loss Comm Delay: {config.LossCommDelay}");
            Console.Write("\nEnter number to edit (0 to exit): ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= 6)
            {
                switch (choice)
                {
                    case 1:
                        Console.Write("Enter Failsafe Function (0-3): ");
                        if (int.TryParse(Console.ReadLine(), out int failsafeFunc))
                            config.FailsafeFunction = (FunctionAction)failsafeFunc;
                        break;
                    case 2:
                        Console.Write("Enter Failsafe Go To Position (0-100): ");
                        if (byte.TryParse(Console.ReadLine(), out byte failsafePos))
                            config.FailsafeGoToPosition = failsafePos;
                        break;
                    case 3:
                        Console.Write("Enter ESD Function (0-3): ");
                        if (int.TryParse(Console.ReadLine(), out int esdFunc))
                            config.EsdFunction = (FunctionAction)esdFunc;
                        break;
                    case 4:
                        Console.Write("Enter ESD Delay (0-255): ");
                        if (byte.TryParse(Console.ReadLine(), out byte esdDelay))
                            config.EsdDelay = esdDelay;
                        break;
                    case 5:
                        Console.Write("Enter Loss Comm Function (0-3): ");
                        if (int.TryParse(Console.ReadLine(), out int lossCommFunc))
                            config.LossCommFunction = (FunctionAction)lossCommFunc;
                        break;
                    case 6:
                        Console.Write("Enter Loss Comm Delay (0-255): ");
                        if (byte.TryParse(Console.ReadLine(), out byte lossCommDelay))
                            config.LossCommDelay = lossCommDelay;
                        break;
                }
                Console.WriteLine("\nUpdated.");
            }
        }

        static void EditNetworkSettings(DeviceConfig config)
        {
            Console.WriteLine($"\n--- Network Settings ---");
            Console.WriteLine($"1. Network Baud Rate: {config.NetworkBaudRate}");
            Console.WriteLine($"2. Network Response Delay: {config.NetworkResponseDelay}");
            Console.WriteLine($"3. Network Comm Parity: {config.NetworkCommParity}");
            Console.Write("\nEnter number to edit (0 to exit): ");

            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= 3)
            {
                switch (choice)
                {
                    case 1:
                        Console.Write("Enter Network Baud Rate (0-5): ");
                        if (int.TryParse(Console.ReadLine(), out int baudRate))
                            config.NetworkBaudRate = (NetworkBaudRate)baudRate;
                        break;
                    case 2:
                        Console.Write("Enter Network Response Delay (0-255): ");
                        if (byte.TryParse(Console.ReadLine(), out byte responseDelay))
                            config.NetworkResponseDelay = responseDelay;
                        break;
                    case 3:
                        Console.Write("Enter Network Comm Parity (0-2): ");
                        if (int.TryParse(Console.ReadLine(), out int parity))
                            config.NetworkCommParity = (NetworkCommParity)parity;
                        break;
                }
                Console.WriteLine("\nUpdated.");
            }
        }

    }
}
