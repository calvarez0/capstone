using System;
using System.Collections.Generic;
using System.IO.Ports;
using NModbus;
using NModbus.Serial;

namespace ModbusActuatorControl
{
    // Unified Modbus RTU Master for both hardware and simulation modes
    public class ModbusMaster : IActuatorMaster
    {
        private readonly bool _isSimulation;

        // Hardware mode fields
        private SerialPort? _serialPort;
        private NModbus.IModbusMaster? _nmodbusmaster;

        // Simulation mode fields
        private readonly Dictionary<byte, ModbusSlaveSimulator> _slaves = new Dictionary<byte, ModbusSlaveSimulator>();

        private bool _isConnected;

        public bool IsConnected => _isConnected;

        // Connection settings
        public string ComPort { get; private set; }
        public int BaudRate { get; private set; }
        public int Parity { get; private set; }
        public int StopBits { get; private set; }

        // Constructor for hardware mode
        public ModbusMaster(string portName, int baudRate = 9600, int dataBits = 8,
                           System.IO.Ports.Parity parity = System.IO.Ports.Parity.None,
                           System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One)
        {
            _isSimulation = false;
            ComPort = portName;
            BaudRate = baudRate;
            Parity = (int)parity;
            StopBits = (int)stopBits;

            _serialPort = new SerialPort(portName)
            {
                BaudRate = baudRate,
                DataBits = dataBits,
                Parity = parity,
                StopBits = stopBits,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
        }

        // Constructor for simulation mode
        public ModbusMaster(string portName, int baudRate, int parity, int stopBits, bool isSimulation)
        {
            if (!isSimulation)
                throw new ArgumentException("Use the other constructor for hardware mode");

            _isSimulation = true;
            ComPort = portName;
            BaudRate = baudRate;
            Parity = parity;
            StopBits = stopBits;
        }

        // Connect to device or start simulation
        public void Connect()
        {
            if (_isConnected)
                return;

            if (_isSimulation)
            {
                _isConnected = true;
                Console.WriteLine($"[Simulator] Connected to virtual Modbus network");
            }
            else
            {
                try
                {
                    _serialPort!.Open();
                    var factory = new ModbusFactory();
                    var adapter = new SerialPortAdapter(_serialPort);
                    _nmodbusmaster = factory.CreateRtuMaster(adapter);
                    _isConnected = true;
                    Console.WriteLine($"Connected to {_serialPort.PortName}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to connect: {ex.Message}", ex);
                }
            }
        }

        // Disconnect from device or stop simulation
        public void Disconnect()
        {
            if (!_isConnected)
                return;

            if (_isSimulation)
            {
                foreach (var slave in _slaves.Values)
                {
                    slave.StopSimulation();
                }
                _isConnected = false;
                Console.WriteLine($"[Simulator] Disconnected from virtual Modbus network");
            }
            else
            {
                try
                {
                    _nmodbusmaster?.Dispose();
                    _serialPort?.Close();
                    _isConnected = false;
                    Console.WriteLine("Disconnected");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during disconnect: {ex.Message}");
                }
            }
        }

        // Simulation mode: Add a simulated slave device
        public void AddSlave(byte slaveId, ushort initialPosition = 0)
        {
            if (!_isSimulation)
                throw new InvalidOperationException("AddSlave is only available in simulation mode");

            if (_slaves.ContainsKey(slaveId))
            {
                Console.WriteLine($"[Simulator] Slave {slaveId} already exists");
                return;
            }

            var slave = new ModbusSlaveSimulator(slaveId, initialPosition);
            slave.StartSimulation();
            _slaves[slaveId] = slave;
            Console.WriteLine($"[Simulator] Added slave device {slaveId}");
        }

        // Simulation mode: Remove a simulated slave device
        public bool RemoveSlave(byte slaveId)
        {
            if (!_isSimulation)
                throw new InvalidOperationException("RemoveSlave is only available in simulation mode");

            if (_slaves.TryGetValue(slaveId, out var slave))
            {
                slave.StopSimulation();
                _slaves.Remove(slaveId);
                Console.WriteLine($"[Simulator] Removed slave device {slaveId}");
                return true;
            }
            return false;
        }

        // Simulation mode: Get all slave IDs
        public List<byte> GetSlaveIds()
        {
            if (!_isSimulation)
                throw new InvalidOperationException("GetSlaveIds is only available in simulation mode");

            return new List<byte>(_slaves.Keys);
        }

        // Simulation mode: Clear all slaves
        public void ClearSlaves()
        {
            if (!_isSimulation)
                throw new InvalidOperationException("ClearSlaves is only available in simulation mode");

            foreach (var slave in _slaves.Values)
            {
                slave.StopSimulation();
            }
            _slaves.Clear();
            Console.WriteLine($"[Simulator] Cleared all slave devices");
        }

        // Read holding registers from the device
        public ushort[] ReadHoldingRegisters(byte slaveId, ushort startAddress, ushort count)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected to device");

            try
            {
                if (_isSimulation)
                {
                    if (!_slaves.TryGetValue(slaveId, out var slave))
                        throw new InvalidOperationException($"Slave {slaveId} not found");
                    return slave.ReadHoldingRegisters(startAddress, count);
                }
                else
                {
                    return _nmodbusmaster!.ReadHoldingRegisters(slaveId, startAddress, count);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read holding registers: {ex.Message}", ex);
            }
        }

        // Write single holding register to the device
        public void WriteSingleRegister(byte slaveId, ushort address, ushort value)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected to device");

            try
            {
                if (_isSimulation)
                {
                    if (!_slaves.TryGetValue(slaveId, out var slave))
                        throw new InvalidOperationException($"Slave {slaveId} not found");
                    slave.WriteSingleRegister(address, value);
                }
                else
                {
                    _nmodbusmaster!.WriteSingleRegister(slaveId, address, value);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write register: {ex.Message}", ex);
            }
        }

        // Read coils from the device
        public bool[] ReadCoils(byte slaveId, ushort startAddress, ushort count)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected to device");

            try
            {
                if (_isSimulation)
                {
                    if (!_slaves.TryGetValue(slaveId, out var slave))
                        throw new InvalidOperationException($"Slave {slaveId} not found");
                    return slave.ReadCoils(startAddress, count);
                }
                else
                {
                    return _nmodbusmaster!.ReadCoils(slaveId, startAddress, count);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read coils: {ex.Message}", ex);
            }
        }

        // Write single coil to the device
        public void WriteSingleCoil(byte slaveId, ushort address, bool value)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Not connected to device");

            try
            {
                if (_isSimulation)
                {
                    if (!_slaves.TryGetValue(slaveId, out var slave))
                        throw new InvalidOperationException($"Slave {slaveId} not found");
                    slave.WriteSingleCoil(address, value);
                }
                else
                {
                    _nmodbusmaster!.WriteSingleCoil(slaveId, address, value);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write coil: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            Disconnect();
            _serialPort?.Dispose();
        }
    }
}
