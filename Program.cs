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
        private static SystemConfiguration _currentConfig;
        private static bool _isSimulationMode = false;

        static void Main(string[] args)
        {
            Console.WriteLine("=== Modbus RTU Actuator Control System ===\n");

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
                Console.WriteLine("Simulated devices already created. Clear them first.");
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

            // Create simulator master and connect
            _master = new ModbusMaster("SIM", baudRate, parity, stopBits, isSimulation: true);
            _master.Connect();

            // Add simulated slave devices
            for (byte i = 0; i < count; i++)
            {
                byte slaveId = (byte)(startId + i);
                _master.AddSlave(slaveId, (ushort)(i * 100));
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
                _currentConfig = new SystemConfiguration();
            }
            _currentConfig.ComPort = "SIM";
            _currentConfig.BaudRate = baudRate;
            _currentConfig.Parity = parity;
            _currentConfig.StopBits = stopBits;

            Console.WriteLine($"\nCreated {count} simulated device(s)");
            Console.WriteLine($"Connection settings: {baudRate} baud, Parity={parity}, StopBits={stopBits}");
            Console.WriteLine($"Found {_devices.Count} device(s)");
        }

        static void ListSimulatedDevices()
        {
            if (_devices.Count == 0)
            {
                Console.WriteLine("\nNo simulated devices. Create some first.");
                return;
            }

            Console.WriteLine("\nSimulated Devices:");
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

            Console.WriteLine("All simulated devices cleared");
        }

        static void SwitchMode()
        {
            Console.WriteLine("\nSwitching mode will clear all current devices.");
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
                Console.WriteLine($"\nSwitched to {(_isSimulationMode ? "SIMULATION" : "HARDWARE")} MODE");
            }
        }

        static void ConnectToPort()
        {
            if (_master != null && _master.IsConnected)
            {
                Console.WriteLine("Already connected. Disconnect first.");
                return;
            }

            Console.WriteLine("\nAvailable COM ports:");
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
                Console.WriteLine($"Connected successfully!");
                Console.WriteLine($"Settings: {baudRate} baud, Parity={parity}, StopBits={stopBits}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
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

            Console.WriteLine($"\nFound {_devices.Count} device(s)");
        }

        static void MonitorDevices()
        {
            if (!CheckConnection()) return;

            if (_devices.Count == 0)
            {
                Console.WriteLine($"\nNo devices found. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
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
                Console.WriteLine($"\nNo devices available. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
                return;
            }

            Console.WriteLine($"\n--- Send Commands {(_isSimulationMode ? "(Simulation)" : "")} ---");
            Console.Write("Enter slave ID: ");
            byte slaveId = byte.Parse(Console.ReadLine());

            var device = _devices.FirstOrDefault(d => d.SlaveId == slaveId);
            if (device == null)
            {
                Console.WriteLine($"Device {slaveId} not found.");
                return;
            }

            Console.WriteLine("\n1. Move to Position (0-4095)");
            Console.WriteLine("2. Set Torque (Close & Open)");
            Console.WriteLine("3. Stop");
            Console.WriteLine("4. Enable/Disable");
            Console.WriteLine("5. Reset Errors");
            Console.WriteLine("6. Manage Bit Flags");
            Console.Write("\nSelect command: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        Console.Write("Enter target position (0-4095): ");
                        ushort position = ushort.Parse(Console.ReadLine());
                        device.MoveToPosition(position);
                        break;
                    case "2":
                        Console.Write("Enter close torque (15-100, default 50): ");
                        ushort closeTorque = ushort.Parse(Console.ReadLine());
                        Console.Write("Enter open torque (15-100, default 50): ");
                        ushort openTorque = ushort.Parse(Console.ReadLine());
                        device.SetTorque(closeTorque, openTorque);
                        break;
                    case "3":
                        device.Stop();
                        break;
                    case "4":
                        Console.Write("Enable? (y/n): ");
                        bool enable = Console.ReadLine().ToLower() == "y";
                        device.SetEnabled(enable);
                        break;
                    case "5":
                        device.ResetErrors();
                        break;
                    case "6":
                        ManageBitFlagsMenu(device);
                        break;
                    default:
                        Console.WriteLine("Invalid command");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command failed: {ex.Message}");
            }
        }

        static void ManageBitFlagsMenu(ActuatorDevice device)
        {
            while (true)
            {
                Console.WriteLine($"\n--- Manage Bit Flags - Device {device.SlaveId} ---");
                Console.WriteLine("1. Register 11 Flags");
                Console.WriteLine("2. Register 12 Flags");
                Console.WriteLine("0. Return to previous menu");
                Console.Write("\nSelect register: ");

                var choice = Console.ReadLine();

                if (choice == "0") break;

                switch (choice)
                {
                    case "1":
                        ManageRegister11Menu(device);
                        break;
                    case "2":
                        ManageRegister12Menu(device);
                        break;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
            }
        }

        static void ManageRegister11Menu(ActuatorDevice device)
        {
            try
            {
                Console.WriteLine($"\n--- Manage Register 11 Flags - Device {device.SlaveId} ---");

                // Read current flags from device
                var flags = device.ReadBitFlags();

                while (true)
                {
                    Console.WriteLine("\n" + flags.ToString());
                    Console.WriteLine("\n--- Edit Bit Flags ---");
                    Console.WriteLine("1.  EHO-Type");
                    Console.WriteLine("2.  Local Input Function");
                    Console.WriteLine("3.  Remote Input Function");
                    Console.WriteLine("4.  Remote ESD Enabled");
                    Console.WriteLine("5.  Loss Comm Enabled");
                    Console.WriteLine("6.  AI1 Polarity");
                    Console.WriteLine("7.  AI2 Polarity");
                    Console.WriteLine("8.  AO1 Polarity");
                    Console.WriteLine("9.  AO2 Polarity");
                    Console.WriteLine("10. DI1 Open Trigger");
                    Console.WriteLine("11. DI2 Close Trigger");
                    Console.WriteLine("12. DI3 Stop Trigger");
                    Console.WriteLine("13. DI4 ESD Trigger");
                    Console.WriteLine("14. DI5 PST Trigger");
                    Console.WriteLine("15. Close Direction");
                    Console.WriteLine("16. Seat");
                    Console.WriteLine("17. Write flags to device");
                    Console.WriteLine("18. Reset all flags to defaults");
                    Console.WriteLine("0.  Return to previous menu");
                    Console.Write("\nSelect option: ");

                    var choice = Console.ReadLine();

                    if (choice == "0") break;

                    switch (choice)
                    {
                        case "1":
                            Console.WriteLine($"Current: {flags.EhoType}");
                            Console.WriteLine("0 = Double Action, 1 = Spring Return");
                            Console.Write("Enter value (0 or 1): ");
                            flags.EhoType = Console.ReadLine() == "1" ? EhoType.SpringReturn : EhoType.DoubleAction;
                            break;
                        case "2":
                            Console.WriteLine($"Current: {flags.LocalInputFunction}");
                            Console.WriteLine("0 = Maintained, 1 = Momentary");
                            Console.Write("Enter value (0 or 1): ");
                            flags.LocalInputFunction = Console.ReadLine() == "1" ? InputFunction.Momentary : InputFunction.Maintained;
                            break;
                        case "3":
                            Console.WriteLine($"Current: {flags.RemoteInputFunction}");
                            Console.WriteLine("0 = Maintained, 1 = Momentary");
                            Console.Write("Enter value (0 or 1): ");
                            flags.RemoteInputFunction = Console.ReadLine() == "1" ? InputFunction.Momentary : InputFunction.Maintained;
                            break;
                        case "4":
                            Console.WriteLine($"Current: {flags.RemoteEsdEnabled}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.RemoteEsdEnabled = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "5":
                            Console.WriteLine($"Current: {flags.LossCommEnabled}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.LossCommEnabled = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "6":
                            Console.WriteLine($"Current: {flags.Ai1Polarity}");
                            Console.WriteLine("0 = Normal, 1 = Reversed");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Ai1Polarity = Console.ReadLine() == "1" ? Polarity.Reversed : Polarity.Normal;
                            break;
                        case "7":
                            Console.WriteLine($"Current: {flags.Ai2Polarity}");
                            Console.WriteLine("0 = Normal, 1 = Reversed");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Ai2Polarity = Console.ReadLine() == "1" ? Polarity.Reversed : Polarity.Normal;
                            break;
                        case "8":
                            Console.WriteLine($"Current: {flags.Ao1Polarity}");
                            Console.WriteLine("0 = Normal, 1 = Reversed");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Ao1Polarity = Console.ReadLine() == "1" ? Polarity.Reversed : Polarity.Normal;
                            break;
                        case "9":
                            Console.WriteLine($"Current: {flags.Ao2Polarity}");
                            Console.WriteLine("0 = Normal, 1 = Reversed");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Ao2Polarity = Console.ReadLine() == "1" ? Polarity.Reversed : Polarity.Normal;
                            break;
                        case "10":
                            Console.WriteLine($"Current: {flags.Di1OpenTrigger}");
                            Console.WriteLine("0 = Normally Open, 1 = Normally Close");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Di1OpenTrigger = Console.ReadLine() == "1" ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
                            break;
                        case "11":
                            Console.WriteLine($"Current: {flags.Di2CloseTrigger}");
                            Console.WriteLine("0 = Normally Open, 1 = Normally Close");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Di2CloseTrigger = Console.ReadLine() == "1" ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
                            break;
                        case "12":
                            Console.WriteLine($"Current: {flags.Di3StopTrigger}");
                            Console.WriteLine("0 = Normally Open, 1 = Normally Close");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Di3StopTrigger = Console.ReadLine() == "1" ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
                            break;
                        case "13":
                            Console.WriteLine($"Current: {flags.Di4EsdTrigger}");
                            Console.WriteLine("0 = Normally Open, 1 = Normally Close");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Di4EsdTrigger = Console.ReadLine() == "1" ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
                            break;
                        case "14":
                            Console.WriteLine($"Current: {flags.Di5PstTrigger}");
                            Console.WriteLine("0 = Normally Open, 1 = Normally Close");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Di5PstTrigger = Console.ReadLine() == "1" ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
                            break;
                        case "15":
                            Console.WriteLine($"Current: {flags.CloseDirection}");
                            Console.WriteLine("0 = Clockwise, 1 = Counter-Clockwise");
                            Console.Write("Enter value (0 or 1): ");
                            flags.CloseDirection = Console.ReadLine() == "1" ? CloseDirection.CounterClockwise : CloseDirection.Clockwise;
                            break;
                        case "16":
                            Console.WriteLine($"Current: {flags.Seat}");
                            Console.WriteLine("0 = Position, 1 = Torque");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Seat = Console.ReadLine() == "1" ? SeatMode.Torque : SeatMode.Position;
                            break;
                        case "17":
                            device.SetBitFlags(flags);
                            Console.WriteLine("Flags written to device successfully!");
                            break;
                        case "18":
                            flags = new Register11BitFlags();
                            Console.WriteLine("All flags reset to defaults");
                            break;
                        default:
                            Console.WriteLine("Invalid option");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error managing bit flags: {ex.Message}");
            }
        }

        static void ManageRegister12Menu(ActuatorDevice device)
        {
            try
            {
                Console.WriteLine($"\n--- Manage Register 12 Flags - Device {device.SlaveId} ---");

                // Read current flags from device
                var flags = device.ReadRegister12Flags();

                while (true)
                {
                    Console.WriteLine("\n" + flags.ToString());
                    Console.WriteLine("\n--- Edit Register 12 Flags ---");
                    Console.WriteLine("1.  Torque Backseat");
                    Console.WriteLine("2.  Torque Retry");
                    Console.WriteLine("3.  Remote Display");
                    Console.WriteLine("4.  LEDs");
                    Console.WriteLine("5.  Open Inhibit");
                    Console.WriteLine("6.  Close Inhibit");
                    Console.WriteLine("7.  Local ESD");
                    Console.WriteLine("8.  ESD O-R Thermal");
                    Console.WriteLine("9.  ESD O-R Local");
                    Console.WriteLine("10. ESD O-R Stop");
                    Console.WriteLine("11. ESD O-R Inhibit");
                    Console.WriteLine("12. ESD O-R Torque");
                    Console.WriteLine("13. Close Speed Control");
                    Console.WriteLine("14. Open Speed Control");
                    Console.WriteLine("15. Write flags to device");
                    Console.WriteLine("16. Reset all flags to defaults");
                    Console.WriteLine("0.  Return to previous menu");
                    Console.Write("\nSelect option: ");

                    var choice = Console.ReadLine();

                    if (choice == "0") break;

                    switch (choice)
                    {
                        case "1":
                            Console.WriteLine($"Current: {flags.TorqueBackseat}");
                            Console.WriteLine("0 = Off, 1 = On");
                            Console.Write("Enter value (0 or 1): ");
                            flags.TorqueBackseat = Console.ReadLine() == "1" ? OnOffState.On : OnOffState.Off;
                            break;
                        case "2":
                            Console.WriteLine($"Current: {flags.TorqueRetry}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.TorqueRetry = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "3":
                            Console.WriteLine($"Current: {flags.RemoteDisplay}");
                            Console.WriteLine("0 = Off, 1 = On");
                            Console.Write("Enter value (0 or 1): ");
                            flags.RemoteDisplay = Console.ReadLine() == "1" ? OnOffState.On : OnOffState.Off;
                            break;
                        case "4":
                            Console.WriteLine($"Current: {flags.Leds}");
                            Console.WriteLine("0 = Close-Green/Open-Red, 1 = Close-Red/Open-Green");
                            Console.Write("Enter value (0 or 1): ");
                            flags.Leds = Console.ReadLine() == "1" ? LedColorScheme.CloseRedOpenGreen : LedColorScheme.CloseGreenOpenRed;
                            break;
                        case "5":
                            Console.WriteLine($"Current: {flags.OpenInhibit}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.OpenInhibit = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "6":
                            Console.WriteLine($"Current: {flags.CloseInhibit}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.CloseInhibit = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "7":
                            Console.WriteLine($"Current: {flags.LocalEsd}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.LocalEsd = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "8":
                            Console.WriteLine($"Current: {flags.EsdOrThermal}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.EsdOrThermal = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "9":
                            Console.WriteLine($"Current: {flags.EsdOrLocal}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.EsdOrLocal = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "10":
                            Console.WriteLine($"Current: {flags.EsdOrStop}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.EsdOrStop = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "11":
                            Console.WriteLine($"Current: {flags.EsdOrInhibit}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.EsdOrInhibit = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "12":
                            Console.WriteLine($"Current: {flags.EsdOrTorque}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.EsdOrTorque = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "13":
                            Console.WriteLine($"Current: {flags.CloseSpeedControl}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.CloseSpeedControl = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "14":
                            Console.WriteLine($"Current: {flags.OpenSpeedControl}");
                            Console.WriteLine("0 = Disabled, 1 = Enabled");
                            Console.Write("Enter value (0 or 1): ");
                            flags.OpenSpeedControl = Console.ReadLine() == "1" ? EnabledState.Enabled : EnabledState.Disabled;
                            break;
                        case "15":
                            device.SetRegister12Flags(flags);
                            Console.WriteLine("Flags written to device successfully!");
                            break;
                        case "16":
                            flags = new Register12BitFlags();
                            Console.WriteLine("All flags reset to defaults");
                            break;
                        default:
                            Console.WriteLine("Invalid option");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error managing Register 12 flags: {ex.Message}");
            }
        }

        static void ConfigurationMenu()
        {
            Console.WriteLine("\n--- Configuration Management ---");
            Console.WriteLine("1. Create New Configuration");
            Console.WriteLine("2. Load Configuration from File");
            Console.WriteLine("3. Save Current Configuration");
            Console.WriteLine("4. Apply Configuration to Devices");
            Console.WriteLine("5. Read Configuration from Devices");
            Console.WriteLine("6. Export Configuration to CSV");
            Console.Write("\nSelect option: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        CreateNewConfiguration();
                        break;
                    case "2":
                        LoadConfiguration();
                        break;
                    case "3":
                        SaveConfiguration();
                        break;
                    case "4":
                        ApplyConfiguration();
                        break;
                    case "5":
                        ReadConfigurationFromDevices();
                        break;
                    case "6":
                        ExportConfigurationToCsv();
                        break;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Configuration operation failed: {ex.Message}");
            }
        }

        static void CreateNewConfiguration()
        {
            _currentConfig = ConfigurationManager.CreateDefaultConfiguration();
            Console.WriteLine("Default configuration created.");
        }

        static void LoadConfiguration()
        {
            Console.Write("Enter configuration file path: ");
            var filePath = Console.ReadLine();
            _currentConfig = ConfigurationManager.LoadConfiguration(filePath);
            Console.WriteLine($"Configuration loaded: {_currentConfig.Actuators.Count} actuator(s)");
            Console.WriteLine($"Connection Settings: {_currentConfig.ComPort}, {_currentConfig.BaudRate} baud, Parity={_currentConfig.Parity}, StopBits={_currentConfig.StopBits}");

            if (!_isSimulationMode && (_master == null || !_master.IsConnected))
            {
                Console.Write("\nConnect using these settings? (y/n): ");
                if (Console.ReadLine()?.ToLower() == "y")
                {
                    try
                    {
                        var parity = GetParityFromInt(_currentConfig.Parity);
                        var stopBits = GetStopBitsFromInt(_currentConfig.StopBits);
                        _master = new ModbusMaster(_currentConfig.ComPort, _currentConfig.BaudRate, 8, parity, stopBits);
                        _master.Connect();
                        Console.WriteLine("Connected successfully!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Connection failed: {ex.Message}");
                    }
                }
            }
        }

        static void SaveConfiguration()
        {
            if (_currentConfig == null)
            {
                Console.WriteLine("No configuration to save. Create or load one first.");
                return;
            }

            Console.Write("Enter save file path (e.g., config.json): ");
            var filePath = Console.ReadLine();
            ConfigurationManager.SaveConfiguration(_currentConfig, filePath);
        }

        static void ApplyConfiguration()
        {
            if (_currentConfig == null)
            {
                Console.WriteLine("No configuration loaded. Load one first.");
                return;
            }

            if (!CheckConnection()) return;

            var master = _master;
            ConfigurationManager.ApplyConfiguration(_currentConfig, master);
        }

        static void ReadConfigurationFromDevices()
        {
            if (!CheckConnection()) return;

            if (_devices.Count == 0)
            {
                Console.WriteLine($"No devices found. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
                return;
            }

            var master = _master;
            var slaveIds = _devices.Select(d => d.SlaveId).ToList();
            _currentConfig = ConfigurationManager.ReadConfigurationFromDevices(master, slaveIds);
            Console.WriteLine($"Configuration read from {(_isSimulationMode ? "simulated " : "")}devices successfully");
        }

        static void ExportConfigurationToCsv()
        {
            if (_currentConfig == null)
            {
                Console.WriteLine("No configuration to export. Create or load one first.");
                return;
            }

            Console.Write("Enter CSV file path (e.g., config.csv): ");
            var filePath = Console.ReadLine();
            ConfigurationManager.ExportToCsv(_currentConfig, filePath);
        }

        static void CalibrationMenu()
        {
            if (!CheckConnection()) return;

            if (_devices.Count == 0)
            {
                Console.WriteLine($"\nNo devices available. {(_isSimulationMode ? "Create simulated devices" : "Run 'Scan for Devices'")} first.");
                return;
            }

            Console.Write("\nEnter slave ID to calibrate: ");
            byte slaveId = byte.Parse(Console.ReadLine());

            var device = _devices.FirstOrDefault(d => d.SlaveId == slaveId);
            if (device == null)
            {
                Console.WriteLine($"Device {slaveId} not found.");
                return;
            }

            Console.WriteLine($"\nStarting calibration for {(_isSimulationMode ? "simulated " : "")}device {slaveId}...");
            if (_isSimulationMode)
                Console.WriteLine("(Simulation will take ~3 seconds)");
            else
                Console.WriteLine("WARNING: Ensure the actuator path is clear!");
            Console.Write("Continue? (y/n): ");

            if (Console.ReadLine().ToLower() == "y")
            {
                try
                {
                    device.StartCalibration();
                    Console.WriteLine("Calibration command sent. Monitor device status.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Calibration failed: {ex.Message}");
                }
            }
        }

        static void Disconnect()
        {
            if (_master == null || !_master.IsConnected)
            {
                Console.WriteLine("Not connected.");
                return;
            }

            foreach (var device in _devices)
            {
                device.StopPolling();
            }
            _devices.Clear();

            _master.Disconnect();
            _master = null;
            Console.WriteLine("Disconnected successfully.");
        }

        static void Cleanup()
        {
            foreach (var device in _devices)
            {
                device.StopPolling();
            }
            _master?.Dispose();
        }

        static bool CheckConnection()
        {
            if (_master == null || !_master.IsConnected)
            {
                if (_isSimulationMode)
                    Console.WriteLine("\nNo simulated devices. Create simulated devices first.");
                else
                    Console.WriteLine("\nNot connected. Connect to a COM port first.");
                return false;
            }
            return true;
        }

        static Parity GetParityFromInt(int value)
        {
            return value switch
            {
                1 => Parity.Even,
                2 => Parity.Odd,
                _ => Parity.None
            };
        }

        static StopBits GetStopBitsFromInt(int value)
        {
            return value == 2 ? StopBits.Two : StopBits.One;
        }
    }
}
