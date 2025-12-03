using System;
using System.Threading;
using System.Threading.Tasks;
using WPF_GUI.Models;

namespace WPF_GUI.Services
{
    /// <summary>
    /// Simulates a Modbus actuator device for UI testing without hardware
    /// </summary>
    public class SimulationService
    {
        private DeviceState _deviceState;
        private ActuatorConfiguration _config;
        private Timer _simulationTimer;
        private Random _random;

        private bool _isMoving;
        private double _targetPosition;
        private string _currentCommand; // "open", "close", or "stop"

        public event EventHandler<string> StatusChanged;

        public SimulationService(DeviceState deviceState, ActuatorConfiguration config)
        {
            _deviceState = deviceState;
            _config = config;
            _random = new Random();

            // Initialize with some default values
            _deviceState.Position = 50;
            _deviceState.Torque = 45;
            _deviceState.CurrentStatus = "Stopped";
            _deviceState.PowerOK = true;
            _deviceState.Communication = true;
            _deviceState.Calibrated = true;
            _deviceState.Moving = false;
            _deviceState.OpenLimit = false;
            _deviceState.CloseLimit = false;

            _targetPosition = _deviceState.Position;
            _currentCommand = "stop";
        }

        public void Start()
        {
            // Start simulation timer (runs every 100ms)
            _simulationTimer = new Timer(SimulationTick, null, 0, 100);
            StatusChanged?.Invoke(this, "Simulation mode active");
        }

        public void Stop()
        {
            _simulationTimer?.Dispose();
            _simulationTimer = null;
        }

        private void SimulationTick(object state)
        {
            // Simulate device behavior
            UpdatePosition();
            UpdateTorque();
            UpdateStatus();
        }

        private void UpdatePosition()
        {
            if (_isMoving)
            {
                double moveSpeed = _config.SpeedSetting / 100.0 * 2.0; // 2% per tick at max speed

                if (_currentCommand == "open")
                {
                    _deviceState.Position += moveSpeed;
                    if (_deviceState.Position >= 100)
                    {
                        _deviceState.Position = 100;
                        _isMoving = false;
                        _deviceState.Moving = false;
                        _deviceState.OpenLimit = true;
                        _deviceState.CloseLimit = false;
                        _deviceState.CurrentStatus = "Open";
                    }
                    else
                    {
                        _deviceState.CurrentStatus = "Opening";
                    }
                }
                else if (_currentCommand == "close")
                {
                    _deviceState.Position -= moveSpeed;
                    if (_deviceState.Position <= 0)
                    {
                        _deviceState.Position = 0;
                        _isMoving = false;
                        _deviceState.Moving = false;
                        _deviceState.OpenLimit = false;
                        _deviceState.CloseLimit = true;
                        _deviceState.CurrentStatus = "Closed";
                    }
                    else
                    {
                        _deviceState.CurrentStatus = "Closing";
                    }
                }
            }
        }

        private void UpdateTorque()
        {
            if (_isMoving)
            {
                // Simulate torque variation during movement (40-60% with some randomness)
                _deviceState.Torque = 40 + _random.NextDouble() * 20;
            }
            else
            {
                // Lower torque when stopped
                _deviceState.Torque = 20 + _random.NextDouble() * 10;
            }

            // Clamp torque to limits
            if (_deviceState.Torque > _config.TorqueLimit)
            {
                _deviceState.Torque = _config.TorqueLimit;
            }
        }

        private void UpdateStatus()
        {
            _deviceState.Moving = _isMoving;

            // Simulate status determination
            if (!_isMoving)
            {
                if (_deviceState.Position >= 99)
                {
                    _deviceState.CurrentStatus = "Open";
                    _deviceState.OpenLimit = true;
                    _deviceState.CloseLimit = false;
                }
                else if (_deviceState.Position <= 1)
                {
                    _deviceState.CurrentStatus = "Closed";
                    _deviceState.OpenLimit = false;
                    _deviceState.CloseLimit = true;
                }
                else
                {
                    _deviceState.CurrentStatus = "Stopped";
                    _deviceState.OpenLimit = false;
                    _deviceState.CloseLimit = false;
                }
            }
        }

        // Command methods
        public void SendOpenCommand()
        {
            if (_deviceState.Position < 100)
            {
                _currentCommand = "open";
                _isMoving = true;
                _deviceState.Moving = true;
                _deviceState.CurrentStatus = "Opening";
                StatusChanged?.Invoke(this, "Open command sent");
            }
        }

        public void SendCloseCommand()
        {
            if (_deviceState.Position > 0)
            {
                _currentCommand = "close";
                _isMoving = true;
                _deviceState.Moving = true;
                _deviceState.CurrentStatus = "Closing";
                StatusChanged?.Invoke(this, "Close command sent");
            }
        }

        public void SendStopCommand()
        {
            _currentCommand = "stop";
            _isMoving = false;
            _deviceState.Moving = false;
            _deviceState.CurrentStatus = "Stopped";
            StatusChanged?.Invoke(this, "Stop command sent");
        }

        // Configuration methods for Page 3
        public void UpdateConfiguration(ActuatorConfiguration config)
        {
            _config = config;
            StatusChanged?.Invoke(this, "Configuration updated");
        }

        public void EnterSetupMode()
        {
            // In simulation, just allow it
            StatusChanged?.Invoke(this, "Entered Setup Mode");
        }

        public void ExitSetupMode()
        {
            StatusChanged?.Invoke(this, "Exited Setup Mode");
        }

        public bool IsInSetupMode()
        {
            // In simulation, always return true
            return true;
        }

        // Network simulation for Page 4
        public DeviceState[] SimulateNetwork(int deviceCount)
        {
            DeviceState[] devices = new DeviceState[deviceCount];

            for (int i = 0; i < deviceCount; i++)
            {
                devices[i] = new DeviceState
                {
                    Position = _random.NextDouble() * 100,
                    Torque = 30 + _random.NextDouble() * 40,
                    PowerOK = _random.NextDouble() > 0.1, // 90% chance of being powered
                    Communication = _random.NextDouble() > 0.2, // 80% chance of communication
                    Calibrated = true,
                    Moving = _random.NextDouble() > 0.7,
                    CurrentStatus = "Active"
                };
            }

            return devices;
        }
    }
}
