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
        private bool _isEnabled = true;

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

            // Initialize Register 10 (Host Commands) to default (all zeros)
            _holdingRegisters[10] = 0;

            // Initialize Register 11 (Bit Flags) to default (all zeros)
            _holdingRegisters[11] = 0;

            // Initialize Register 12 (Bit Flags) to default (all zeros)
            _holdingRegisters[12] = 0;

            // Initialize Control Functions Registers 101-102 to defaults
            // Register 101: UH = Control Mode (0 = 2-Wire Discrete), LH = Modulation Delay (1)
            _holdingRegisters[101] = (ushort)((0 << 8) | 1); // Control Mode = 0, Modulation Delay = 1
            // Register 102: UH = Deadband (20), LH = Network Adapter (0 = None)
            _holdingRegisters[102] = (ushort)((20 << 8) | 0); // Deadband = 20, Network Adapter = 0

            // Initialize Relay Registers 103-107 to default (all zeros)
            for (ushort i = 103; i <= 107; i++)
            {
                _holdingRegisters[i] = 0;
            }

            // Initialize Additional Functions Registers 107-110
            // Register 107 UH: Failsafe Function (0 = Stay Put)
            _holdingRegisters[107] = 0;
            // Register 108: UH = Failsafe Go To Position (50), LH = ESD Function (0 = Stay Put)
            _holdingRegisters[108] = (ushort)((50 << 8) | 0);
            // Register 109: UH = ESD Delay (0), LH = Loss Comm Function (0 = Stay Put)
            _holdingRegisters[109] = 0;
            // Register 110: UH = Loss Comm Delay (0), LH = Network Baud Rate (3 = 9600)
            _holdingRegisters[110] = (ushort)((0 << 8) | 3);

            // Initialize Network Info Registers 111
            // Register 111: UH = Network Response Delay (8), LH = Network Comm Parity (0 = None)
            _holdingRegisters[111] = (ushort)((8 << 8) | 0);
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
                    // Process Register 10 Host Commands
                    ProcessHostCommands();

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

                    // Auto-clear Host Open/Close/ESD commands when target reached
                    if (_currentPosition == _targetPosition)
                    {
                        var reg10 = _holdingRegisters[10];
                        bool hostOpenCmd = (reg10 & (1 << 0)) != 0;
                        bool hostCloseCmd = (reg10 & (1 << 1)) != 0;
                        bool hostEsdCmd = (reg10 & (1 << 3)) != 0;

                        if (hostOpenCmd || hostCloseCmd || hostEsdCmd)
                        {
                            reg10 &= unchecked((ushort)~((1 << 0) | (1 << 1) | (1 << 3))); // Clear Open, Close, and ESD bits
                            _holdingRegisters[10] = reg10;
                        }
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

        private void ProcessHostCommands()
        {
            var reg10 = _holdingRegisters[10];
            bool hostOpenCmd = (reg10 & (1 << 0)) != 0;
            bool hostCloseCmd = (reg10 & (1 << 1)) != 0;
            bool hostStopCmd = (reg10 & (1 << 2)) != 0;
            bool hostEsdCmd = (reg10 & (1 << 3)) != 0;

            // Handle Host Stop CMD - takes priority
            if (hostStopCmd)
            {
                _targetPosition = _currentPosition;
                _isMoving = false;
            }
            // Handle Host ESD CMD - second priority
            else if (hostEsdCmd)
            {
                ProcessEsdCommand();
            }
            // Handle Host Open CMD
            else if (hostOpenCmd)
            {
                _targetPosition = 4095; // Max position for now
            }
            // Handle Host Close CMD
            else if (hostCloseCmd)
            {
                _targetPosition = 0; // Min position for now
            }

            // PST command does nothing for now
        }

        private void ProcessEsdCommand()
        {
            // Read ESD Function from Register 108 LH
            var reg108 = _holdingRegisters[108];
            byte esdFunction = (byte)(reg108 & 0xFF);

            switch (esdFunction)
            {
                case 0: // Stay Put
                    _targetPosition = _currentPosition;
                    Console.WriteLine($"[Slave {_slaveId}] ESD: Stay Put");
                    break;
                case 1: // Go Open
                    _targetPosition = 4095;
                    Console.WriteLine($"[Slave {_slaveId}] ESD: Go Open");
                    break;
                case 2: // Go Close
                    _targetPosition = 0;
                    Console.WriteLine($"[Slave {_slaveId}] ESD: Go Close");
                    break;
                case 3: // Go to Position
                    // Read Failsafe Go To Position from Register 108 UH (0-100%)
                    byte failsafePercent = (byte)((reg108 >> 8) & 0xFF);
                    // Convert percentage to 0-4095 range
                    _targetPosition = (ushort)((failsafePercent * 4095) / 100);
                    Console.WriteLine($"[Slave {_slaveId}] ESD: Go to Position {failsafePercent}% (position {_targetPosition})");
                    break;
            }
        }

        private void UpdateStatusRegisters()
        {
            // Register 23: Actual Position
            _holdingRegisters[23] = _currentPosition;

            // Register 3: Operating Status
            // bit 4 = Valve Opening (moving to higher position)
            // bit 5 = Valve Closing (moving to lower position)
            // bit 8 = Stop Mode (based on Register 10 bit 2)
            // bit 9 = Setup Mode / Soft Setup Mode (based on Register 10 bit 15)
            ushort reg3 = 0;
            var reg10 = _holdingRegisters[10];
            bool hostStopCmd = (reg10 & (1 << 2)) != 0;
            bool softSetupCmd = (reg10 & (1 << 15)) != 0;

            if (_isMoving && _currentPosition < _targetPosition) reg3 |= (1 << 4); // Valve Opening
            if (_isMoving && _currentPosition > _targetPosition) reg3 |= (1 << 5); // Valve Closing
            if (hostStopCmd) reg3 |= (1 << 8); // Set Stop Mode bit
            if (softSetupCmd) reg3 |= (1 << 9); // Set Setup Mode bit
            _holdingRegisters[3] = reg3;

            // Register 24: Valve Torque (0-4095 representing 0.024% each)
            // Convert open/close torque (15-100%) to 0-4095 range
            // Formula: torque_value = (torque_percent * 4095) / 100
            ushort currentTorque = 0;
            if (_isMoving)
            {
                if (_currentPosition < _targetPosition)
                {
                    // Opening: use open torque
                    currentTorque = (ushort)((_openTorque * 4095) / 100);
                }
                else if (_currentPosition > _targetPosition)
                {
                    // Closing: use close torque
                    currentTorque = (ushort)((_closeTorque * 4095) / 100);
                }
            }
            _holdingRegisters[24] = currentTorque;

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
                case 10: // Register 10 - Command flags
                    // Check the stop command bit
                    bool stopCmd = (value & (1 << 2)) != 0;
                    if (stopCmd)
                    {
                        // Stop movement immediately
                        _targetPosition = _currentPosition;
                        _isMoving = false;
                    }
                    // The value is stored in _holdingRegisters[10] at the beginning
                    // Other command bits are handled elsewhere or reflected in status
                    break;

                case 20: // Setpoint Position
                    if (value > 4095)
                    {
                        Console.WriteLine($"[Slave {_slaveId}] Position {value} out of range [0-4095]");
                    }
                    else
                    {
                        _targetPosition = value;
                        Console.WriteLine($"[Slave {_slaveId}] Moving to position {value}");
                    }
                    break;

                case 11: // Register 11 - Bit Flags
                    // The value is already stored in _holdingRegisters[11] at the beginning of this method
                    break;

                case 12: // Register 12 - Bit Flags
                    // The value is already stored in _holdingRegisters[12] at the beginning of this method
                    break;

                case 101: // Control Functions Register 101 (UH: Control Mode, LH: Modulation Delay)
                    // Value is already stored in _holdingRegisters[101]
                    break;

                case 102: // Control Functions Register 102 (UH: Deadband, LH: Network Adapter)
                    // Value is already stored in _holdingRegisters[102]
                    break;

                case 112: // Torque (UB: Close, LB: Open)
                    _closeTorque = (ushort)((value >> 8) & 0xFF);
                    _openTorque = (ushort)(value & 0xFF);

                    if (_closeTorque < 15 || _closeTorque > 100)
                    {
                        _closeTorque = Math.Max((ushort)15, Math.Min((ushort)100, _closeTorque));
                    }
                    if (_openTorque < 15 || _openTorque > 100)
                    {
                        _openTorque = Math.Max((ushort)15, Math.Min((ushort)100, _openTorque));
                    }
                    break;

                case 200: // Calibration Command
                    if (value == 1)
                    {
                        Console.WriteLine($"[Slave {_slaveId}] Calibration started");
                        Task.Run(() =>
                        {
                            Thread.Sleep(3000);
                            _currentPosition = 0;
                            _targetPosition = 0;
                            Console.WriteLine($"[Slave {_slaveId}] Calibration complete");
                        });
                    }
                    break;

                case 201: // Reset Errors
                    if (value == 1)
                    {
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
