using System.Configuration;
using System.Data;
using System.Windows;
using System.Collections.Generic;
using ModbusActuatorControl;

namespace WPF_GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Backend integration - ModbusMaster handles both hardware and simulation
        public static ModbusMaster? Master { get; private set; }
        public static List<ActuatorDevice> Devices { get; private set; } = new List<ActuatorDevice>();
        public static SystemConfig? CurrentConfig { get; set; }

        public static bool IsSimulationMode { get; private set; } = true; // Default to simulation
        public static bool IsConnected => Master != null && Master.IsConnected;

        public static event EventHandler<bool>? ModeChanged;
        public static event EventHandler<string>? StatusChanged;
        public static event EventHandler<ActuatorDevice>? DeviceStatusUpdated;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize with default configuration
            CurrentConfig = new SystemConfig();

            // Start in simulation mode with a default device
            CreateSimulatedDevices(9600, 0, 1, 1, 1, ProductCapabilities.PRODUCT_S7X);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up resources
            Cleanup();
            base.OnExit(e);
        }

        public static void SetMode(bool isSimulation)
        {
            if (IsSimulationMode == isSimulation)
                return; // No change

            // Clear existing devices and connections
            Cleanup();

            IsSimulationMode = isSimulation;

            if (isSimulation)
            {
                // Create default simulation
                CreateSimulatedDevices(9600, 0, 1, 1, 1, ProductCapabilities.PRODUCT_S7X);
            }

            // Notify all pages that mode has changed
            ModeChanged?.Invoke(null, isSimulation);
            StatusChanged?.Invoke(null, isSimulation ? "Simulation mode active" : "Hardware mode active");
        }

        public static void CreateSimulatedDevices(int baudRate, int parity, int stopBits, byte startId, int count, ushort productIdentifier)
        {
            Cleanup();

            Master = new ModbusMaster("SIM", baudRate, parity, stopBits, isSimulation: true);
            Master.Connect();

            // Add simulated slave devices
            for (byte i = 0; i < count; i++)
            {
                byte slaveId = (byte)(startId + i);
                Master.AddSlave(slaveId, (ushort)(i * 100), productIdentifier);
            }

            // Create ActuatorDevice instances for each slave
            Devices.Clear();
            var slaveIds = Master.GetSlaveIds();
            foreach (var slaveId in slaveIds)
            {
                var device = new ActuatorDevice(Master, slaveId);
                device.StatusUpdated += Device_StatusUpdated;
                Devices.Add(device);
            }

            // Initialize config
            CurrentConfig = new SystemConfig
            {
                ComPort = "SIM",
                BaudRate = baudRate,
                Parity = parity,
                StopBits = stopBits
            };

            // Read configuration from simulated devices
            foreach (var device in Devices)
            {
                try
                {
                    var config = device.ReadConfiguration();
                    CurrentConfig.Actuators.Add(config);
                }
                catch { }
            }

            StatusChanged?.Invoke(null, $"Created {count} simulated device(s) - Product: {ProductCapabilities.GetProductName(productIdentifier)}");
        }

        public static bool ConnectToPort(string portName, int baudRate, System.IO.Ports.Parity parity, System.IO.Ports.StopBits stopBits)
        {
            if (IsSimulationMode)
            {
                StatusChanged?.Invoke(null, "Cannot connect to hardware in simulation mode. Switch to hardware mode first.");
                return false;
            }

            try
            {
                Cleanup();

                Master = new ModbusMaster(portName, baudRate, 8, parity, stopBits);
                Master.Connect();

                CurrentConfig = new SystemConfig
                {
                    ComPort = portName,
                    BaudRate = baudRate,
                    Parity = (int)parity,
                    StopBits = (int)stopBits
                };

                StatusChanged?.Invoke(null, $"Connected to {portName} ({baudRate} baud)");
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(null, $"Connection failed: {ex.Message}");
                return false;
            }
        }

        public static void ScanDevices(byte startId, byte endId)
        {
            if (Master == null || !Master.IsConnected)
            {
                StatusChanged?.Invoke(null, "Not connected. Connect first.");
                return;
            }

            Devices.Clear();

            for (byte id = startId; id <= endId; id++)
            {
                try
                {
                    var testDevice = new ActuatorDevice(Master, id);
                    testDevice.UpdateStatus();
                    testDevice.StatusUpdated += Device_StatusUpdated;
                    Devices.Add(testDevice);
                    StatusChanged?.Invoke(null, $"Device found at slave ID {id}");
                }
                catch
                {
                    // Device not found at this ID
                }
            }

            StatusChanged?.Invoke(null, $"Scan complete. Found {Devices.Count} device(s).");
        }

        private static void Device_StatusUpdated(object? sender, ActuatorStatus status)
        {
            if (sender is ActuatorDevice device)
            {
                DeviceStatusUpdated?.Invoke(null, device);
            }
        }

        public static void Cleanup()
        {
            foreach (var device in Devices)
            {
                device.StopPolling();
                device.StatusUpdated -= Device_StatusUpdated;
            }
            Devices.Clear();

            if (Master != null)
            {
                Master.Disconnect();
                Master = null;
            }
        }

        public static void Disconnect()
        {
            Cleanup();
            StatusChanged?.Invoke(null, "Disconnected");
        }

        // Get the first available device (convenience method)
        public static ActuatorDevice? GetCurrentDevice()
        {
            return Devices.Count > 0 ? Devices[0] : null;
        }

        // Get device by slave ID
        public static ActuatorDevice? GetDevice(byte slaveId)
        {
            return Devices.Find(d => d.SlaveId == slaveId);
        }
    }
}
