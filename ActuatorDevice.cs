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

                // Read Product Identifier from Register 100
                status.ProductIdentifier = _master.ReadHoldingRegisters(_slaveId, 100, 1)[0];

                // Read torque settings from Register 112
                var torqueReg = _master.ReadHoldingRegisters(_slaveId, 112, 1)[0];
                status.CloseTorque = (ushort)((torqueReg >> 8) & 0xFF);
                status.OpenTorque = (ushort)(torqueReg & 0xFF);

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

                // Read Product Identifier
                config.ProductIdentifier = _master.ReadHoldingRegisters(_slaveId, 100, 1)[0];

                // Read torque (if available for this product)
                if (ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 112))
                {
                    var torqueReg = _master.ReadHoldingRegisters(_slaveId, 112, 1)[0];
                    config.CloseTorque = (ushort)((torqueReg >> 8) & 0xFF);
                    config.OpenTorque = (ushort)(torqueReg & 0xFF);
                }

                // Read PST Result (if available for this product)
                if (ProductCapabilities.IsRegisterAvailable(config.ProductIdentifier, 29))
                {
                    config.PstResult = (byte)_master.ReadHoldingRegisters(_slaveId, 29, 1)[0];
                }

                // Read all device configuration (pass product identifier for filtering)
                config.Config.ReadFromDevice(_master, _slaveId, config.ProductIdentifier);

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

        // Product Identifier from Register 100
        public ushort ProductIdentifier { get; set; }

        // Torque settings from Register 112
        public ushort CloseTorque { get; set; }
        public ushort OpenTorque { get; set; }

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

            // Convert PST result to readable string
            string pstResultText = Status.PstResult switch
            {
                0 => "Never Run",
                1 => "In Progress",
                2 => "Passed",
                3 => "Failed",
                _ => $"Unknown ({Status.PstResult})"
            };

            // Convert Product Identifier to readable string
            string productName = ProductCapabilities.GetProductName(ProductIdentifier);

            var details = $"=== Actuator Status (Updated: {Timestamp:HH:mm:ss}) ===\n" +
                         $"Product: {productName} (0x{ProductIdentifier:X4})\n" +
                         $"Position: {Position}\n";

            // Only show torque if register 112 is available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 112))
            {
                details += $"Close Torque: {CloseTorque}% (Register 112)\n";
                details += $"Open Torque: {OpenTorque}% (Register 112)\n";
            }

            // Only show valve torque if register 24 is available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 24))
            {
                details += $"Valve Torque: {Status.ValveTorque} ({torquePercent:F2}%)\n";
            }

            // Only show PST result if register 29 is available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 29))
            {
                details += $"PST Result: {pstResultText}\n";
            }

            details += $"Analog Input 1: {Status.AnalogInput1}\n";
            details += $"Analog Input 2: {Status.AnalogInput2}\n";
            details += $"Analog Output 1: {Status.AnalogOutput1}\n";

            // Only show Analog Output 2 if register 28 is available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 28))
            {
                details += $"Analog Output 2: {Status.AnalogOutput2}\n";
            }

            details += $"Moving: {IsMoving}\n";
            details += $"Has Alarms: {HasAnyAlarm}\n\n";

            // Alarms section - filter based on available bits
            string alarmsSection = "";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 0, 0))
                alarmsSection += $"Stall Alarm: {Status.StallAlarm}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 0, 1))
                alarmsSection += $"Valve Drift Alarm: {Status.ValveDriftAlarm}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 0, 2))
                alarmsSection += $"ESD Active Alarm: {Status.EsdActiveAlarm}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 0, 3))
                alarmsSection += $"Motor Thermal Alarm: {Status.MotorThermalAlarm}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 1, 8))
                alarmsSection += $"Loss of Power Alarm: {Status.LossOfPowerAlarm}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 1, 9))
                alarmsSection += $"Loss of Signal Alarm: {Status.LossOfSignalAlarm}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 1, 10))
                alarmsSection += $"Low Oil Alarm: {Status.LowOilAlarm}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 1, 11))
                alarmsSection += $"Unit Alarm 1: {Status.UnitAlarm1}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 1, 12))
                alarmsSection += $"Unit Alarm 2: {Status.UnitAlarm2}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 1, 13))
                alarmsSection += $"SPH Exceeded: {Status.SphExceeded}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 1, 14))
                alarmsSection += $"Unit Alert: {Status.UnitAlert}\n";

            if (alarmsSection.Length > 0)
            {
                details += "=== Alarms (Register 0-2) ===\n";
                details += alarmsSection + "\n";
            }

            // Operating Status (Register 3) - filter based on available bits
            string operatingSection = "";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 0))
                operatingSection += $"Limit Switch Open: {Status.LimitSwitchOpen}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 1))
                operatingSection += $"Limit Switch Close: {Status.LimitSwitchClose}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 2))
                operatingSection += $"Torque Switch Open: {Status.TorqueSwitchOpen}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 3))
                operatingSection += $"Torque Switch Close: {Status.TorqueSwitchClose}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 4))
                operatingSection += $"Valve Opening: {Status.ValveOpening}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 5))
                operatingSection += $"Valve Closing: {Status.ValveClosing}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 6))
                operatingSection += $"Local Mode: {Status.LocalMode}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 7))
                operatingSection += $"Remote Mode: {Status.RemoteMode}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 8))
                operatingSection += $"Stop Mode: {Status.StopMode}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 9))
                operatingSection += $"Setup Mode: {Status.SetupMode}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 3, 10))
                operatingSection += $"Handwheel Pulled Out: {Status.HandwheelPulledOut}\n";

            if (operatingSection.Length > 0)
            {
                details += "=== Operating Status (Register 3) ===\n";
                details += operatingSection + "\n";
            }

            // Relay Status (Register 4) - filter based on available bits
            string relaySection = "";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 0))
                relaySection += $"Relay 1: {Status.Relay1Status}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 1))
                relaySection += $"Relay 2: {Status.Relay2Status}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 2))
                relaySection += $"Relay 3: {Status.Relay3Status}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 3))
                relaySection += $"Relay 4: {Status.Relay4Status}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 4))
                relaySection += $"Relay 5 Monitor: {Status.Relay5MonitorStatus}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 5))
                relaySection += $"Relay 6: {Status.Relay6Status}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 6))
                relaySection += $"Relay 7: {Status.Relay7Status}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 7))
                relaySection += $"Relay 8: {Status.Relay8Status}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 8))
                relaySection += $"Relay 9: {Status.Relay9Status}\n";

            // Digital Input Status (Register 4)
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 9))
                relaySection += $"DI1 Open: {Status.Di1OpenStatus}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 10))
                relaySection += $"DI2 Close: {Status.Di2CloseStatus}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 11))
                relaySection += $"DI3 Stop: {Status.Di3StopStatus}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 12))
                relaySection += $"DI4 ESD: {Status.Di4EsdStatus}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 4, 13))
                relaySection += $"DI5 PST: {Status.Di5PstStatus}\n";

            if (relaySection.Length > 0)
            {
                details += "=== Relay & Digital Input Status (Register 4) ===\n";
                details += relaySection + "\n";
            }

            // Host Commands (Register 10) - filter based on available bits
            string hostCmdSection = "";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 10, 0))
                hostCmdSection += $"Host Open Cmd: {Status.HostOpenCmd}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 10, 1))
                hostCmdSection += $"Host Close Cmd: {Status.HostCloseCmd}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 10, 2))
                hostCmdSection += $"Host Stop Cmd: {Status.HostStopCmd}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 10, 3))
                hostCmdSection += $"Host ESD Cmd: {Status.HostEsdCmd}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 10, 4))
                hostCmdSection += $"Host PST Cmd: {Status.HostPstCmd}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 10, 15))
                hostCmdSection += $"Soft Setup Cmd: {Status.SoftSetupCmd}\n";

            if (hostCmdSection.Length > 0)
            {
                details += "=== Host Commands (Register 10) ===\n";
                details += hostCmdSection + "\n";
            }

            // Configuration (Register 11) - filter based on available bits
            string reg11Section = "";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 0))
                reg11Section += $"EHO Type: {Config.EhoType}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 1))
                reg11Section += $"Local Input Function: {Config.LocalInputFunction}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 2))
                reg11Section += $"Remote Input Function: {Config.RemoteInputFunction}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 3))
                reg11Section += $"Remote ESD Enabled: {Config.RemoteEsdEnabled}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 4))
                reg11Section += $"Loss Comm Enabled: {Config.LossCommEnabled}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 5))
                reg11Section += $"AI1 Polarity: {Config.Ai1Polarity}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 6))
                reg11Section += $"AI2 Polarity: {Config.Ai2Polarity}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 7))
                reg11Section += $"AO1 Polarity: {Config.Ao1Polarity}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 8))
                reg11Section += $"AO2 Polarity: {Config.Ao2Polarity}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 9))
                reg11Section += $"DI1 Open Trigger: {Config.Di1OpenTrigger}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 10))
                reg11Section += $"DI2 Close Trigger: {Config.Di2CloseTrigger}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 11))
                reg11Section += $"DI3 Stop Trigger: {Config.Di3StopTrigger}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 12))
                reg11Section += $"DI4 ESD Trigger: {Config.Di4EsdTrigger}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 13))
                reg11Section += $"DI5 PST Trigger: {Config.Di5PstTrigger}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 14))
                reg11Section += $"Close Direction: {Config.CloseDirection}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 11, 15))
                reg11Section += $"Seat Mode: {Config.Seat}\n";

            if (reg11Section.Length > 0)
            {
                details += "=== Configuration (Register 11) ===\n";
                details += reg11Section + "\n";
            }

            // Configuration (Register 12) - filter based on available bits
            string reg12Section = "";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 0))
                reg12Section += $"Torque Backseat: {Config.TorqueBackseat}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 1))
                reg12Section += $"Torque Retry: {Config.TorqueRetry}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 2))
                reg12Section += $"Remote Display: {Config.RemoteDisplay}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 3))
                reg12Section += $"LEDs: {Config.Leds}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 4))
                reg12Section += $"Open Inhibit: {Config.OpenInhibit}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 5))
                reg12Section += $"Close Inhibit: {Config.CloseInhibit}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 6))
                reg12Section += $"Local ESD: {Config.LocalEsd}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 7))
                reg12Section += $"ESD Or Thermal: {Config.EsdOrThermal}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 8))
                reg12Section += $"ESD Or Local: {Config.EsdOrLocal}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 9))
                reg12Section += $"ESD Or Stop: {Config.EsdOrStop}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 10))
                reg12Section += $"ESD Or Inhibit: {Config.EsdOrInhibit}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 11))
                reg12Section += $"ESD Or Torque: {Config.EsdOrTorque}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 12))
                reg12Section += $"Close Speed Control: {Config.CloseSpeedControl}\n";
            if (ProductCapabilities.IsBitAvailable(ProductIdentifier, 12, 13))
                reg12Section += $"Open Speed Control: {Config.OpenSpeedControl}\n";

            if (reg12Section.Length > 0)
            {
                details += "=== Configuration (Register 12) ===\n";
                details += reg12Section + "\n";
            }

            // Control Configuration (Registers 101-102) - only show if available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 101) ||
                ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 102))
            {
                details += "=== Control Configuration (Registers 101-102) ===\n";
                details += $"Control Mode: {Config.ControlMode}\n";
                details += $"Modulation Delay: {Config.ModulationDelay}\n";
                details += $"Deadband: {Config.Deadband}\n";
                details += $"Network Adapter: {Config.NetworkAdapter}\n\n";
            }

            // Additional Functions (Registers 107-110) - build section conditionally
            string additionalFunctionsSection = "";
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 107))
            {
                additionalFunctionsSection += $"Failsafe Function: {Config.FailsafeFunction}\n";
                // Check if lower half of Register 107 is available
                if (ProductCapabilities.IsRegister107LowerHalfAvailable(ProductIdentifier))
                {
                    additionalFunctionsSection += $"Failsafe Go To Position: {Config.FailsafeGoToPosition}\n";
                }
            }
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 108))
            {
                additionalFunctionsSection += $"ESD Function: {Config.EsdFunction}\n";
                additionalFunctionsSection += $"ESD Delay: {Config.EsdDelay}\n";
            }
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 109))
            {
                additionalFunctionsSection += $"Loss Comm Function: {Config.LossCommFunction}\n";
                additionalFunctionsSection += $"Loss Comm Delay: {Config.LossCommDelay}\n";
            }

            if (additionalFunctionsSection.Length > 0)
            {
                details += "=== Additional Functions (Registers 107-110) ===\n";
                details += additionalFunctionsSection + "\n";
            }

            // Network Settings (Registers 110-111) - always available
            details += "=== Network Settings (Registers 110-111) ===\n";
            details += $"Network Baud Rate: {Config.NetworkBaudRate}\n";
            details += $"Network Response Delay: {Config.NetworkResponseDelay}\n";
            details += $"Network Comm Parity: {Config.NetworkCommParity}\n\n";

            // Limit Switch Settings (Register 113) - only show if available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 113))
            {
                details += "=== Limit Switch Settings (Register 113) ===\n";
                details += $"LSA (Limit Switch A): {Config.LSA}%\n";
                details += $"LSB (Limit Switch B): {Config.LSB}%\n\n";
            }

            // Open Speed Control (Register 114) - only show if available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 114))
            {
                details += "=== Open Speed Control (Register 114) ===\n";
                details += $"Open Speed Control Start: {Config.OpenSpeedControlStart}%\n";
                details += $"Open Speed Control Ratio: {Config.OpenSpeedControlRatio}%\n\n";
            }

            // Close Speed Control (Register 115) - only show if available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 115))
            {
                details += "=== Close Speed Control (Register 115) ===\n";
                details += $"Close Speed Control Start: {Config.CloseSpeedControlStart}%\n";
                details += $"Close Speed Control Ratio: {Config.CloseSpeedControlRatio}%\n\n";
            }

            // Calibration Values (Registers 500-507) - only show if available
            if (ProductCapabilities.IsRegisterAvailable(ProductIdentifier, 500))
            {
                details += "=== Calibration Values (Registers 500-507) ===\n";
                details += $"Analog Input 1 Zero Calibration: {Config.AnalogInput1ZeroCalibration} ({Config.AnalogInput1ZeroCalibration * 0.024:F2}%)\n";
                details += $"Analog Input 1 Span Calibration: {Config.AnalogInput1SpanCalibration} ({Config.AnalogInput1SpanCalibration * 0.024:F2}%)\n";
                details += $"Analog Input 2 Zero Calibration: {Config.AnalogInput2ZeroCalibration} ({Config.AnalogInput2ZeroCalibration * 0.024:F2}%)\n";
                details += $"Analog Input 2 Span Calibration: {Config.AnalogInput2SpanCalibration} ({Config.AnalogInput2SpanCalibration * 0.024:F2}%)\n";
                details += $"Analog Output 1 Zero Calibration: {Config.AnalogOutput1ZeroCalibration} ({Config.AnalogOutput1ZeroCalibration * 0.024:F2}%)\n";
                details += $"Analog Output 1 Span Calibration: {Config.AnalogOutput1SpanCalibration} ({Config.AnalogOutput1SpanCalibration * 0.024:F2}%)\n";
                details += $"Analog Output 2 Zero Calibration: {Config.AnalogOutput2ZeroCalibration} ({Config.AnalogOutput2ZeroCalibration * 0.024:F2}%)\n";
                details += $"Analog Output 2 Span Calibration: {Config.AnalogOutput2SpanCalibration} ({Config.AnalogOutput2SpanCalibration * 0.024:F2}%)\n";
            }

            return details;
        }
    }
}
