using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusActuatorControl
{
    // Electric actuator device with polling and command capabilities
    public class ActuatorDevice
    {
        private readonly IActuatorMaster _master;
        private readonly byte _slaveId;
        private CancellationTokenSource _pollCancellationTokenSource;
        private Task _pollTask;

        public byte SlaveId => _slaveId;
        public ActuatorStatus CurrentStatus { get; private set; }
        public event EventHandler<ActuatorStatus> StatusUpdated;

        public ActuatorDevice(IActuatorMaster master, byte slaveId)
        {
            _master = master ?? throw new ArgumentNullException(nameof(master));
            _slaveId = slaveId;
            CurrentStatus = new ActuatorStatus();
        }

        // Start polling the device for status updates
        public void StartPolling(int pollIntervalMs = 1000)
        {
            if (_pollTask != null && !_pollTask.IsCompleted)
            {
                Console.WriteLine($"Polling already active for device {_slaveId}");
                return;
            }

            _pollCancellationTokenSource = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoop(pollIntervalMs, _pollCancellationTokenSource.Token));
            Console.WriteLine($"Started polling device {_slaveId}");
        }

        // Stop polling the device
        public void StopPolling()
        {
            if (_pollCancellationTokenSource != null)
            {
                _pollCancellationTokenSource.Cancel();
                _pollTask?.Wait(2000);
                Console.WriteLine($"Stopped polling device {_slaveId}");
            }
        }

        private async Task PollLoop(int intervalMs, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    UpdateStatus();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error polling device {_slaveId}: {ex.Message}");
                }

                try
                {
                    await Task.Delay(intervalMs, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        // Manually update the device status
        public void UpdateStatus()
        {
            try
            {
                var statusRegisters = _master.ReadHoldingRegisters(_slaveId, 0, 24);

                var status = new ActuatorStatus
                {
                    Position = statusRegisters[23],
                    Torque = statusRegisters[2],
                    ErrorCode = statusRegisters[4],
                    OperatingMode = statusRegisters[5],
                    IsMoving = (statusRegisters[6] & 0x01) != 0,
                    IsCalibrated = (statusRegisters[6] & 0x02) != 0,
                    HasError = statusRegisters[4] != 0,
                    Timestamp = DateTime.Now
                };

                CurrentStatus = status;
                StatusUpdated?.Invoke(this, status);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update status for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Move actuator to a specific position (0-4095)
        public void MoveToPosition(ushort position)
        {
            try
            {
                if (position > 4095)
                    throw new ArgumentException("Position must be between 0 and 4095");

                _master.WriteSingleRegister(_slaveId, 20, position);
                Console.WriteLine($"Device {_slaveId}: Moving to position {position}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to move device {_slaveId} to position: {ex.Message}", ex);
            }
        }

        // Set actuator torque (close and open)
        public void SetTorque(ushort closeTorque, ushort openTorque)
        {
            try
            {
                if (closeTorque < 15 || closeTorque > 100)
                    throw new ArgumentException("Close torque must be between 15 and 100");
                if (openTorque < 15 || openTorque > 100)
                    throw new ArgumentException("Open torque must be between 15 and 100");

                ushort combinedTorque = (ushort)((closeTorque << 8) | openTorque);
                _master.WriteSingleRegister(_slaveId, 112, combinedTorque);
                Console.WriteLine($"Device {_slaveId}: Torque set - Close: {closeTorque}, Open: {openTorque}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set torque for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Set actuator torque limit (legacy - sets both close and open to same value)
        public void SetTorqueLimit(ushort torque)
        {
            SetTorque(torque, torque);
        }

        // Start calibration procedure
        public void StartCalibration()
        {
            try
            {
                _master.WriteSingleRegister(_slaveId, 200, 1);
                Console.WriteLine($"Device {_slaveId}: Calibration started");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start calibration for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Stop actuator movement
        public void Stop()
        {
            try
            {
                _master.WriteSingleRegister(_slaveId, 103, 1);
                Console.WriteLine($"Device {_slaveId}: Stopped");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to stop device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Enable or disable the actuator
        public void SetEnabled(bool enabled)
        {
            try
            {
                _master.WriteSingleCoil(_slaveId, 0, enabled);
                Console.WriteLine($"Device {_slaveId}: {(enabled ? "Enabled" : "Disabled")}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set enabled state for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Reset error state
        public void ResetErrors()
        {
            try
            {
                _master.WriteSingleRegister(_slaveId, 201, 1);
                Console.WriteLine($"Device {_slaveId}: Errors reset");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to reset errors for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Apply a configuration to the device
        public void ApplyConfiguration(ActuatorConfiguration config)
        {
            try
            {
                Console.WriteLine($"Applying configuration to device {_slaveId}...");

                SetTorque(config.CloseTorque, config.OpenTorque);
                Thread.Sleep(50);

                Console.WriteLine($"Configuration applied successfully to device {_slaveId}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to apply configuration to device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Read current configuration from the device
        public ActuatorConfiguration ReadConfiguration()
        {
            try
            {
                var configRegisters = _master.ReadHoldingRegisters(_slaveId, 112, 1);

                ushort torqueRegister = configRegisters[0];
                ushort closeTorque = (ushort)((torqueRegister >> 8) & 0xFF);
                ushort openTorque = (ushort)(torqueRegister & 0xFF);

                return new ActuatorConfiguration
                {
                    SlaveId = _slaveId,
                    CloseTorque = closeTorque > 0 ? closeTorque : (ushort)50,
                    OpenTorque = openTorque > 0 ? openTorque : (ushort)50,
                    MinPosition = 0,
                    MaxPosition = 4095
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read configuration from device {_slaveId}: {ex.Message}", ex);
            }
        }
    }

    // Current status of an actuator
    public class ActuatorStatus
    {
        public ushort Position { get; set; }
        public ushort Torque { get; set; }
        public ushort ErrorCode { get; set; }
        public ushort OperatingMode { get; set; }
        public bool IsMoving { get; set; }
        public bool IsCalibrated { get; set; }
        public bool HasError { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return $"Position: {Position}, Torque: {Torque}, " +
                   $"Error: {ErrorCode}, Moving: {IsMoving}, Calibrated: {IsCalibrated}";
        }
    }
}
