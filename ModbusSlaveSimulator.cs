using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusActuatorControl
{
    // Simulates a Modbus RTU slave device with register-level accuracy
    public class ModbusSlaveSimulator
    {
        private readonly byte _slaveId;
        private readonly ushort[] _holdingRegisters = new ushort[256];
        private readonly bool[] _coils = new bool[16];
        private readonly Random _random = new Random();

        // Internal state
        private ushort _currentPosition;
        private ushort _targetPosition;
        private ushort _closeTorque = 50;
        private ushort _openTorque = 50;
        private bool _isMoving = false;
        private bool _isCalibrated = true;
        private bool _isEnabled = true;
        private ushort _errorCode = 0;
        private ushort _operatingMode = 1;

        private CancellationTokenSource _simulationCts;
        private Task _simulationTask;

        public byte SlaveId => _slaveId;

        public ModbusSlaveSimulator(byte slaveId, ushort initialPosition = 0)
        {
            _slaveId = slaveId;
            _currentPosition = initialPosition;
            _targetPosition = initialPosition;
            InitializeRegisters();
        }

        private void InitializeRegisters()
        {
            // Initialize status registers
            UpdateStatusRegisters();

            // Initialize control register defaults
            _holdingRegisters[112] = (ushort)((_closeTorque << 8) | _openTorque);

            // Initialize Register 11 (Bit Flags) to default (all zeros)
            _holdingRegisters[11] = 0;

            // Initialize Register 12 (Bit Flags) to default (all zeros)
            _holdingRegisters[12] = 0;
        }

        public void StartSimulation()
        {
            if (_simulationTask != null && !_simulationTask.IsCompleted)
                return;

            _simulationCts = new CancellationTokenSource();
            _simulationTask = Task.Run(() => SimulationLoop(_simulationCts.Token));
            Console.WriteLine($"[Slave {_slaveId}] Simulation started");
        }

        public void StopSimulation()
        {
            _simulationCts?.Cancel();
            _simulationTask?.Wait(1000);
            Console.WriteLine($"[Slave {_slaveId}] Simulation stopped");
        }

        private async Task SimulationLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Simulate movement
                    if (_isEnabled && _currentPosition != _targetPosition)
                    {
                        _isMoving = true;
                        int step;

                        if (_currentPosition < _targetPosition)
                        {
                            step = _openTorque;
                            _currentPosition = (ushort)Math.Min(_currentPosition + step, _targetPosition);
                        }
                        else
                        {
                            step = _closeTorque;
                            _currentPosition = (ushort)Math.Max(_currentPosition - step, _targetPosition);
                        }
                    }
                    else
                    {
                        _isMoving = false;
                    }

                    // Update status registers
                    UpdateStatusRegisters();

                    await Task.Delay(100, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private void UpdateStatusRegisters()
        {
            // Register 23: Actual Position
            _holdingRegisters[23] = _currentPosition;

            // Register 2: Torque (shows active torque during movement)
            if (_isMoving)
            {
                _holdingRegisters[2] = _currentPosition < _targetPosition ? _openTorque : _closeTorque;
            }
            else
            {
                _holdingRegisters[2] = 0;
            }

            // Register 4: Error Code
            _holdingRegisters[4] = _errorCode;

            // Register 5: Operating Mode
            _holdingRegisters[5] = _operatingMode;

            // Register 6: Status Flags
            ushort flags = 0;
            if (_isMoving) flags |= 0x01;
            if (_isCalibrated) flags |= 0x02;
            _holdingRegisters[6] = flags;

            // Coil 0: Enable state
            _coils[0] = _isEnabled;
        }

        // Modbus RTU Interface - Read Holding Registers
        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort count)
        {
            if (startAddress + count > _holdingRegisters.Length)
                throw new ArgumentException("Register address out of range");

            ushort[] result = new ushort[count];
            Array.Copy(_holdingRegisters, startAddress, result, 0, count);
            return result;
        }

        // Modbus RTU Interface - Write Single Register
        public void WriteSingleRegister(ushort address, ushort value)
        {
            _holdingRegisters[address] = value;

            // Handle control registers
            switch (address)
            {
                case 20: // Setpoint Position
                    if (value > 4095)
                    {
                        _errorCode = 1;
                        Console.WriteLine($"[Slave {_slaveId}] Position {value} out of range [0-4095]");
                    }
                    else
                    {
                        _targetPosition = value;
                        _errorCode = 0;
                        Console.WriteLine($"[Slave {_slaveId}] Moving to position {value}");
                    }
                    break;

                case 11: // Register 11 - Bit Flags
                    Console.WriteLine($"[Slave {_slaveId}] Bit flags updated (Register 11 = 0x{value:X4})");
                    // The value is already stored in _holdingRegisters[11] at the beginning of this method
                    // Just log the update for visibility
                    break;

                case 12: // Register 12 - Bit Flags
                    Console.WriteLine($"[Slave {_slaveId}] Register 12 flags updated (Register 12 = 0x{value:X4})");
                    // The value is already stored in _holdingRegisters[12] at the beginning of this method
                    // Just log the update for visibility
                    break;

                case 103: // Stop Command
                    if (value == 1)
                    {
                        _targetPosition = _currentPosition;
                        _isMoving = false;
                        Console.WriteLine($"[Slave {_slaveId}] Stopped");
                    }
                    break;

                case 112: // Torque (UB: Close, LB: Open)
                    _closeTorque = (ushort)((value >> 8) & 0xFF);
                    _openTorque = (ushort)(value & 0xFF);

                    if (_closeTorque < 15 || _closeTorque > 100)
                    {
                        Console.WriteLine($"[Slave {_slaveId}] Close torque {_closeTorque} out of range [15-100]");
                        _closeTorque = Math.Max((ushort)15, Math.Min((ushort)100, _closeTorque));
                    }
                    if (_openTorque < 15 || _openTorque > 100)
                    {
                        Console.WriteLine($"[Slave {_slaveId}] Open torque {_openTorque} out of range [15-100]");
                        _openTorque = Math.Max((ushort)15, Math.Min((ushort)100, _openTorque));
                    }

                    Console.WriteLine($"[Slave {_slaveId}] Torque set - Close: {_closeTorque}, Open: {_openTorque}");
                    break;

                case 200: // Calibration Command
                    if (value == 1)
                    {
                        Console.WriteLine($"[Slave {_slaveId}] Calibration started");
                        _isCalibrated = false;
                        Task.Run(() =>
                        {
                            Thread.Sleep(3000);
                            _isCalibrated = true;
                            _currentPosition = 0;
                            _targetPosition = 0;
                            Console.WriteLine($"[Slave {_slaveId}] Calibration complete");
                        });
                    }
                    break;

                case 201: // Reset Errors
                    if (value == 1)
                    {
                        _errorCode = 0;
                        Console.WriteLine($"[Slave {_slaveId}] Errors reset");
                    }
                    break;
            }

            UpdateStatusRegisters();
        }

        // Modbus RTU Interface - Read Coils
        public bool[] ReadCoils(ushort startAddress, ushort count)
        {
            if (startAddress + count > _coils.Length)
                throw new ArgumentException("Coil address out of range");

            bool[] result = new bool[count];
            Array.Copy(_coils, startAddress, result, 0, count);
            return result;
        }

        // Modbus RTU Interface - Write Single Coil
        public void WriteSingleCoil(ushort address, bool value)
        {
            _coils[address] = value;

            // Handle control coils
            switch (address)
            {
                case 0: // Enable/Disable
                    _isEnabled = value;
                    if (!value)
                    {
                        _targetPosition = _currentPosition;
                        _isMoving = false;
                    }
                    Console.WriteLine($"[Slave {_slaveId}] {(value ? "Enabled" : "Disabled")}");
                    break;
            }

            UpdateStatusRegisters();
        }
    }
}
