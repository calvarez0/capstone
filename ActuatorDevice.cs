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
                    Timestamp = DateTime.Now
                };

                // Read all status flags
                status.Status.ReadFromDevice(_master, _slaveId);

                // Read configuration
                status.Config.ReadFromDevice(_master, _slaveId);

                CurrentStatus = status;
                StatusUpdated?.Invoke(this, status);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update status for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Move to a specific position
        public void MoveToPosition(ushort position)
        {
            try
            {
                if (position > 4095)
                    throw new ArgumentOutOfRangeException(nameof(position), "Position must be 0-4095");

                _master.WriteSingleRegister(_slaveId, 20, position);
                Console.WriteLine($"Device {_slaveId}: Move to position {position}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to move device {_slaveId} to position {position}: {ex.Message}", ex);
            }
        }

        // Set torque values
        public void SetTorque(byte closeTorque, byte openTorque)
        {
            try
            {
                if (closeTorque < 15 || closeTorque > 100)
                    throw new ArgumentOutOfRangeException(nameof(closeTorque), "Close torque must be 15-100");
                if (openTorque < 15 || openTorque > 100)
                    throw new ArgumentOutOfRangeException(nameof(openTorque), "Open torque must be 15-100");

                ushort torqueValue = (ushort)((closeTorque << 8) | openTorque);
                _master.WriteSingleRegister(_slaveId, 112, torqueValue);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set torque for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Run calibration
        public void Calibrate()
        {
            try
            {
                _master.WriteSingleRegister(_slaveId, 200, 1);
                Console.WriteLine($"Device {_slaveId}: Starting calibration");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to calibrate device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Stop the actuator
        public void Stop(bool enable)
        {
            try
            {
                // Read current command state first to preserve other bits
                UpdateStatus();
                CurrentStatus.Status.HostStopCmd = enable;
                CurrentStatus.Status.WriteCommandsToDevice(_master, _slaveId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to {(enable ? "enable" : "disable")} stop mode for device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Reset errors
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

        // Enable/disable the actuator
        public void SetEnabled(bool enabled)
        {
            try
            {
                _master.WriteSingleCoil(_slaveId, 0, enabled);
                Console.WriteLine($"Device {_slaveId}: {(enabled ? "Enabled" : "Disabled")}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to {(enabled ? "enable" : "disable")} device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Apply full configuration to device
        public void ApplyConfiguration(ActuatorConfig config)
        {
            try
            {
                // Check if setup mode is on before allowing configuration changes
                UpdateStatus();
                if (!CurrentStatus.Status.SetupMode)
                {
                    throw new Exception("Cannot apply configuration. Device must be in setup mode first (use Toggle Setup Mode command).");
                }

                Console.WriteLine($"Applying configuration to device {_slaveId}...");

                // Set torque
                SetTorque((byte)config.CloseTorque, (byte)config.OpenTorque);

                // Write all device configuration
                config.Config.WriteToDevice(_master, _slaveId);

                Console.WriteLine($"Configuration applied to device {_slaveId}.");

                // Automatically exit setup mode after config is applied
                // Read current state and only clear the soft setup bit
                UpdateStatus();
                CurrentStatus.Status.SoftSetupCmd = false;
                CurrentStatus.Status.WriteCommandsToDevice(_master, _slaveId);
                Console.WriteLine($"Exiting setup mode for device {_slaveId}.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to apply configuration to device {_slaveId}: {ex.Message}", ex);
            }
        }

        // Read full configuration from device
        public ActuatorConfig ReadConfiguration()
        {
            try
            {
                var config = new ActuatorConfig
                {
                    SlaveId = _slaveId,
                    DeviceName = $"Actuator {_slaveId}"
                };

                // Read torque
                var torqueReg = _master.ReadHoldingRegisters(_slaveId, 112, 1)[0];
                config.CloseTorque = (ushort)((torqueReg >> 8) & 0xFF);
                config.OpenTorque = (ushort)(torqueReg & 0xFF);

                // Read all device configuration
                config.Config.ReadFromDevice(_master, _slaveId);

                return config;
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
        public DateTime Timestamp { get; set; }

        // All device status (alarms, operating state, etc.)
        public DeviceStatus Status { get; set; } = new DeviceStatus();

        // All device configuration
        public DeviceConfig Config { get; set; } = new DeviceConfig();

        // Convenience properties
        public bool IsMoving => Status.IsMoving();
        public bool HasAnyAlarm => Status.HasAnyAlarm();

        public override string ToString()
        {
            double torquePercent = Status.ValveTorque * 0.024;
            return $"Position: {Position}, Torque: {Status.ValveTorque} ({torquePercent:F2}%), Moving: {IsMoving}, Alarms: {HasAnyAlarm}";
        }

        public string ToDetailedString()
        {
            // Convert valve torque from 0-4095 to percentage (0.024% per unit)
            double torquePercent = Status.ValveTorque * 0.024;

            var details = $"=== Actuator Status (Updated: {Timestamp:HH:mm:ss}) ===\n" +
                         $"Position: {Position}\n" +
                         $"Valve Torque: {Status.ValveTorque} ({torquePercent:F2}%)\n" +
                         $"Moving: {IsMoving}\n" +
                         $"Has Alarms: {HasAnyAlarm}\n\n";

            details += "=== Alarms (Register 0-2) ===\n";
            details += $"Stall Alarm: {Status.StallAlarm}\n";
            details += $"Valve Drift Alarm: {Status.ValveDriftAlarm}\n";
            details += $"ESD Active Alarm: {Status.EsdActiveAlarm}\n";
            details += $"Motor Thermal Alarm: {Status.MotorThermalAlarm}\n";
            details += $"Loss of Power Alarm: {Status.LossOfPowerAlarm}\n";
            details += $"Loss of Signal Alarm: {Status.LossOfSignalAlarm}\n";
            details += $"Low Oil Alarm: {Status.LowOilAlarm}\n";
            details += $"Unit Alarm 1: {Status.UnitAlarm1}\n";
            details += $"Unit Alarm 2: {Status.UnitAlarm2}\n";
            details += $"SPH Exceeded: {Status.SphExceeded}\n";
            details += $"Unit Alert: {Status.UnitAlert}\n\n";

            details += "=== Operating Status (Register 3) ===\n";
            details += $"Limit Switch Open: {Status.LimitSwitchOpen}\n";
            details += $"Limit Switch Close: {Status.LimitSwitchClose}\n";
            details += $"Torque Switch Open: {Status.TorqueSwitchOpen}\n";
            details += $"Torque Switch Close: {Status.TorqueSwitchClose}\n";
            details += $"Valve Opening: {Status.ValveOpening}\n";
            details += $"Valve Closing: {Status.ValveClosing}\n";
            details += $"Local Mode: {Status.LocalMode}\n";
            details += $"Remote Mode: {Status.RemoteMode}\n";
            details += $"Stop Mode: {Status.StopMode}\n";
            details += $"Setup Mode: {Status.SetupMode}\n";
            details += $"Handwheel Pulled Out: {Status.HandwheelPulledOut}\n\n";

            details += "=== Relay Status (Register 4) ===\n";
            details += $"Relay 1: {Status.Relay1Status}\n";
            details += $"Relay 2: {Status.Relay2Status}\n";
            details += $"Relay 3: {Status.Relay3Status}\n";
            details += $"Relay 4: {Status.Relay4Status}\n";
            details += $"Relay 5 Monitor: {Status.Relay5MonitorStatus}\n";
            details += $"Relay 6: {Status.Relay6Status}\n";
            details += $"Relay 7: {Status.Relay7Status}\n";
            details += $"Relay 8: {Status.Relay8Status}\n";
            details += $"Relay 9: {Status.Relay9Status}\n\n";

            details += "=== Digital Input Status (Register 4) ===\n";
            details += $"DI1 Open: {Status.Di1OpenStatus}\n";
            details += $"DI2 Close: {Status.Di2CloseStatus}\n";
            details += $"DI3 Stop: {Status.Di3StopStatus}\n";
            details += $"DI4 ESD: {Status.Di4EsdStatus}\n";
            details += $"DI5 PST: {Status.Di5PstStatus}\n\n";

            details += "=== Host Commands (Register 10) ===\n";
            details += $"Host Open Cmd: {Status.HostOpenCmd}\n";
            details += $"Host Close Cmd: {Status.HostCloseCmd}\n";
            details += $"Host Stop Cmd: {Status.HostStopCmd}\n";
            details += $"Host ESD Cmd: {Status.HostEsdCmd}\n";
            details += $"Host PST Cmd: {Status.HostPstCmd}\n";
            details += $"Soft Setup Cmd: {Status.SoftSetupCmd}\n\n";

            details += "=== Configuration (Register 11) ===\n";
            details += $"EHO Type: {Config.EhoType}\n";
            details += $"Local Input Function: {Config.LocalInputFunction}\n";
            details += $"Remote Input Function: {Config.RemoteInputFunction}\n";
            details += $"Remote ESD Enabled: {Config.RemoteEsdEnabled}\n";
            details += $"Loss Comm Enabled: {Config.LossCommEnabled}\n";
            details += $"AI1 Polarity: {Config.Ai1Polarity}\n";
            details += $"AI2 Polarity: {Config.Ai2Polarity}\n";
            details += $"AO1 Polarity: {Config.Ao1Polarity}\n";
            details += $"AO2 Polarity: {Config.Ao2Polarity}\n";
            details += $"DI1 Open Trigger: {Config.Di1OpenTrigger}\n";
            details += $"DI2 Close Trigger: {Config.Di2CloseTrigger}\n";
            details += $"DI3 Stop Trigger: {Config.Di3StopTrigger}\n";
            details += $"DI4 ESD Trigger: {Config.Di4EsdTrigger}\n";
            details += $"DI5 PST Trigger: {Config.Di5PstTrigger}\n";
            details += $"Close Direction: {Config.CloseDirection}\n";
            details += $"Seat Mode: {Config.Seat}\n\n";

            details += "=== Configuration (Register 12) ===\n";
            details += $"Torque Backseat: {Config.TorqueBackseat}\n";
            details += $"Torque Retry: {Config.TorqueRetry}\n";
            details += $"Remote Display: {Config.RemoteDisplay}\n";
            details += $"LEDs: {Config.Leds}\n";
            details += $"Open Inhibit: {Config.OpenInhibit}\n";
            details += $"Close Inhibit: {Config.CloseInhibit}\n";
            details += $"Local ESD: {Config.LocalEsd}\n";
            details += $"ESD Or Thermal: {Config.EsdOrThermal}\n";
            details += $"ESD Or Local: {Config.EsdOrLocal}\n";
            details += $"ESD Or Stop: {Config.EsdOrStop}\n";
            details += $"ESD Or Inhibit: {Config.EsdOrInhibit}\n";
            details += $"ESD Or Torque: {Config.EsdOrTorque}\n";
            details += $"Close Speed Control: {Config.CloseSpeedControl}\n";
            details += $"Open Speed Control: {Config.OpenSpeedControl}\n\n";

            details += "=== Control Configuration (Registers 101-102) ===\n";
            details += $"Control Mode: {Config.ControlMode}\n";
            details += $"Modulation Delay: {Config.ModulationDelay}\n";
            details += $"Deadband: {Config.Deadband}\n";
            details += $"Network Adapter: {Config.NetworkAdapter}\n\n";

            details += "=== Additional Functions (Registers 107-110) ===\n";
            details += $"Failsafe Function: {Config.FailsafeFunction}\n";
            details += $"Failsafe Go To Position: {Config.FailsafeGoToPosition}\n";
            details += $"ESD Function: {Config.EsdFunction}\n";
            details += $"ESD Delay: {Config.EsdDelay}\n";
            details += $"Loss Comm Function: {Config.LossCommFunction}\n";
            details += $"Loss Comm Delay: {Config.LossCommDelay}\n\n";

            details += "=== Network Settings (Registers 110-111) ===\n";
            details += $"Network Baud Rate: {Config.NetworkBaudRate}\n";
            details += $"Network Response Delay: {Config.NetworkResponseDelay}\n";
            details += $"Network Comm Parity: {Config.NetworkCommParity}\n";

            return details;
        }
    }
}
