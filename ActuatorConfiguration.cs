using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModbusActuatorControl
{
    // Enums for bit flag options
    public enum EhoType { DoubleAction = 0, SpringReturn = 1 }
    public enum InputFunction { Maintained = 0, Momentary = 1 }
    public enum EnabledState { Disabled = 0, Enabled = 1 }
    public enum Polarity { Normal = 0, Reversed = 1 }
    public enum TriggerType { NormallyOpen = 0, NormallyClose = 1 }
    public enum CloseDirection { Clockwise = 0, CounterClockwise = 1 }
    public enum SeatMode { Position = 0, Torque = 1 }

    // Additional enums for Register 12 flags
    public enum OnOffState { Off = 0, On = 1 }
    public enum LedColorScheme { CloseGreenOpenRed = 0, CloseRedOpenGreen = 1 }

    // Class to manage 16-bit flags stored in Register 11
    public class Register11BitFlags
    {
        // Bit 0: EHO-Type
        public EhoType EhoType { get; set; }

        // Bit 1: Local Input Function
        public InputFunction LocalInputFunction { get; set; }

        // Bit 2: Remote Input Function
        public InputFunction RemoteInputFunction { get; set; }

        // Bit 3: Remote ESD Enabled
        public EnabledState RemoteEsdEnabled { get; set; }

        // Bit 4: Loss Comm Enabled
        public EnabledState LossCommEnabled { get; set; }

        // Bit 5: AI1 Polarity
        public Polarity Ai1Polarity { get; set; }

        // Bit 6: AI2 Polarity
        public Polarity Ai2Polarity { get; set; }

        // Bit 7: AO1 Polarity
        public Polarity Ao1Polarity { get; set; }

        // Bit 8: AO2 Polarity
        public Polarity Ao2Polarity { get; set; }

        // Bit 9: DI1 - Open Trigger
        public TriggerType Di1OpenTrigger { get; set; }

        // Bit 10: DI2 - Close Trigger
        public TriggerType Di2CloseTrigger { get; set; }

        // Bit 11: DI3 - Stop Trigger
        public TriggerType Di3StopTrigger { get; set; }

        // Bit 12: DI4 - ESD Trigger
        public TriggerType Di4EsdTrigger { get; set; }

        // Bit 13: DI5 - PST Trigger
        public TriggerType Di5PstTrigger { get; set; }

        // Bit 14: Close Direction
        public CloseDirection CloseDirection { get; set; }

        // Bit 15: Seat
        public SeatMode Seat { get; set; }

        public Register11BitFlags()
        {
            // Default all to 0 values
            EhoType = EhoType.DoubleAction;
            LocalInputFunction = InputFunction.Maintained;
            RemoteInputFunction = InputFunction.Maintained;
            RemoteEsdEnabled = EnabledState.Disabled;
            LossCommEnabled = EnabledState.Disabled;
            Ai1Polarity = Polarity.Normal;
            Ai2Polarity = Polarity.Normal;
            Ao1Polarity = Polarity.Normal;
            Ao2Polarity = Polarity.Normal;
            Di1OpenTrigger = TriggerType.NormallyOpen;
            Di2CloseTrigger = TriggerType.NormallyOpen;
            Di3StopTrigger = TriggerType.NormallyOpen;
            Di4EsdTrigger = TriggerType.NormallyOpen;
            Di5PstTrigger = TriggerType.NormallyOpen;
            CloseDirection = CloseDirection.Clockwise;
            Seat = SeatMode.Position;
        }

        // Convert the flags to a 16-bit register value
        public ushort ToRegisterValue()
        {
            ushort value = 0;

            if (EhoType == EhoType.SpringReturn) value |= (1 << 0);
            if (LocalInputFunction == InputFunction.Momentary) value |= (1 << 1);
            if (RemoteInputFunction == InputFunction.Momentary) value |= (1 << 2);
            if (RemoteEsdEnabled == EnabledState.Enabled) value |= (1 << 3);
            if (LossCommEnabled == EnabledState.Enabled) value |= (1 << 4);
            if (Ai1Polarity == Polarity.Reversed) value |= (1 << 5);
            if (Ai2Polarity == Polarity.Reversed) value |= (1 << 6);
            if (Ao1Polarity == Polarity.Reversed) value |= (1 << 7);
            if (Ao2Polarity == Polarity.Reversed) value |= (1 << 8);
            if (Di1OpenTrigger == TriggerType.NormallyClose) value |= (1 << 9);
            if (Di2CloseTrigger == TriggerType.NormallyClose) value |= (1 << 10);
            if (Di3StopTrigger == TriggerType.NormallyClose) value |= (1 << 11);
            if (Di4EsdTrigger == TriggerType.NormallyClose) value |= (1 << 12);
            if (Di5PstTrigger == TriggerType.NormallyClose) value |= (1 << 13);
            if (CloseDirection == CloseDirection.CounterClockwise) value |= (1 << 14);
            if (Seat == SeatMode.Torque) value |= (1 << 15);

            return value;
        }

        // Create flags from a 16-bit register value
        public static Register11BitFlags FromRegisterValue(ushort value)
        {
            return new Register11BitFlags
            {
                EhoType = (value & (1 << 0)) != 0 ? EhoType.SpringReturn : EhoType.DoubleAction,
                LocalInputFunction = (value & (1 << 1)) != 0 ? InputFunction.Momentary : InputFunction.Maintained,
                RemoteInputFunction = (value & (1 << 2)) != 0 ? InputFunction.Momentary : InputFunction.Maintained,
                RemoteEsdEnabled = (value & (1 << 3)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                LossCommEnabled = (value & (1 << 4)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                Ai1Polarity = (value & (1 << 5)) != 0 ? Polarity.Reversed : Polarity.Normal,
                Ai2Polarity = (value & (1 << 6)) != 0 ? Polarity.Reversed : Polarity.Normal,
                Ao1Polarity = (value & (1 << 7)) != 0 ? Polarity.Reversed : Polarity.Normal,
                Ao2Polarity = (value & (1 << 8)) != 0 ? Polarity.Reversed : Polarity.Normal,
                Di1OpenTrigger = (value & (1 << 9)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen,
                Di2CloseTrigger = (value & (1 << 10)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen,
                Di3StopTrigger = (value & (1 << 11)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen,
                Di4EsdTrigger = (value & (1 << 12)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen,
                Di5PstTrigger = (value & (1 << 13)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen,
                CloseDirection = (value & (1 << 14)) != 0 ? CloseDirection.CounterClockwise : CloseDirection.Clockwise,
                Seat = (value & (1 << 15)) != 0 ? SeatMode.Torque : SeatMode.Position
            };
        }

        public override string ToString()
        {
            return $"Register 11 Flags (0x{ToRegisterValue():X4}):\n" +
                   $"  EHO-Type: {EhoType}\n" +
                   $"  Local Input Function: {LocalInputFunction}\n" +
                   $"  Remote Input Function: {RemoteInputFunction}\n" +
                   $"  Remote ESD Enabled: {RemoteEsdEnabled}\n" +
                   $"  Loss Comm Enabled: {LossCommEnabled}\n" +
                   $"  AI1 Polarity: {Ai1Polarity}\n" +
                   $"  AI2 Polarity: {Ai2Polarity}\n" +
                   $"  AO1 Polarity: {Ao1Polarity}\n" +
                   $"  AO2 Polarity: {Ao2Polarity}\n" +
                   $"  DI1 Open Trigger: {Di1OpenTrigger}\n" +
                   $"  DI2 Close Trigger: {Di2CloseTrigger}\n" +
                   $"  DI3 Stop Trigger: {Di3StopTrigger}\n" +
                   $"  DI4 ESD Trigger: {Di4EsdTrigger}\n" +
                   $"  DI5 PST Trigger: {Di5PstTrigger}\n" +
                   $"  Close Direction: {CloseDirection}\n" +
                   $"  Seat: {Seat}";
        }
    }

    // Class to manage 16-bit flags stored in Register 12
    public class Register12BitFlags
    {
        // Bit 0: Torque Backseat
        public OnOffState TorqueBackseat { get; set; }

        // Bit 1: Torque Retry
        public EnabledState TorqueRetry { get; set; }

        // Bit 2: Reserved (unused)

        // Bit 3: Remote Display
        public OnOffState RemoteDisplay { get; set; }

        // Bit 4: LEDs
        public LedColorScheme Leds { get; set; }

        // Bit 5: Open Inhibit
        public EnabledState OpenInhibit { get; set; }

        // Bit 6: Close Inhibit
        public EnabledState CloseInhibit { get; set; }

        // Bit 7: Local ESD
        public EnabledState LocalEsd { get; set; }

        // Bit 8: ESD O-R Thermal
        public EnabledState EsdOrThermal { get; set; }

        // Bit 9: ESD O-R Local
        public EnabledState EsdOrLocal { get; set; }

        // Bit 10: ESD O-R Stop
        public EnabledState EsdOrStop { get; set; }

        // Bit 11: ESD O-R Inhibit
        public EnabledState EsdOrInhibit { get; set; }

        // Bit 12: ESD O-R Torque
        public EnabledState EsdOrTorque { get; set; }

        // Bit 13: Close Speed Control
        public EnabledState CloseSpeedControl { get; set; }

        // Bit 14: Open Speed Control
        public EnabledState OpenSpeedControl { get; set; }

        // Bit 15: Reserved (unused)

        public Register12BitFlags()
        {
            // Default all to 0 values
            TorqueBackseat = OnOffState.Off;
            TorqueRetry = EnabledState.Disabled;
            RemoteDisplay = OnOffState.Off;
            Leds = LedColorScheme.CloseGreenOpenRed;
            OpenInhibit = EnabledState.Disabled;
            CloseInhibit = EnabledState.Disabled;
            LocalEsd = EnabledState.Disabled;
            EsdOrThermal = EnabledState.Disabled;
            EsdOrLocal = EnabledState.Disabled;
            EsdOrStop = EnabledState.Disabled;
            EsdOrInhibit = EnabledState.Disabled;
            EsdOrTorque = EnabledState.Disabled;
            CloseSpeedControl = EnabledState.Disabled;
            OpenSpeedControl = EnabledState.Disabled;
        }

        // Convert the flags to a 16-bit register value
        public ushort ToRegisterValue()
        {
            ushort value = 0;

            if (TorqueBackseat == OnOffState.On) value |= (1 << 0);
            if (TorqueRetry == EnabledState.Enabled) value |= (1 << 1);
            if (RemoteDisplay == OnOffState.On) value |= (1 << 3);
            if (Leds == LedColorScheme.CloseRedOpenGreen) value |= (1 << 4);
            if (OpenInhibit == EnabledState.Enabled) value |= (1 << 5);
            if (CloseInhibit == EnabledState.Enabled) value |= (1 << 6);
            if (LocalEsd == EnabledState.Enabled) value |= (1 << 7);
            if (EsdOrThermal == EnabledState.Enabled) value |= (1 << 8);
            if (EsdOrLocal == EnabledState.Enabled) value |= (1 << 9);
            if (EsdOrStop == EnabledState.Enabled) value |= (1 << 10);
            if (EsdOrInhibit == EnabledState.Enabled) value |= (1 << 11);
            if (EsdOrTorque == EnabledState.Enabled) value |= (1 << 12);
            if (CloseSpeedControl == EnabledState.Enabled) value |= (1 << 13);
            if (OpenSpeedControl == EnabledState.Enabled) value |= (1 << 14);

            return value;
        }

        // Create flags from a 16-bit register value
        public static Register12BitFlags FromRegisterValue(ushort value)
        {
            return new Register12BitFlags
            {
                TorqueBackseat = (value & (1 << 0)) != 0 ? OnOffState.On : OnOffState.Off,
                TorqueRetry = (value & (1 << 1)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                RemoteDisplay = (value & (1 << 3)) != 0 ? OnOffState.On : OnOffState.Off,
                Leds = (value & (1 << 4)) != 0 ? LedColorScheme.CloseRedOpenGreen : LedColorScheme.CloseGreenOpenRed,
                OpenInhibit = (value & (1 << 5)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                CloseInhibit = (value & (1 << 6)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                LocalEsd = (value & (1 << 7)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                EsdOrThermal = (value & (1 << 8)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                EsdOrLocal = (value & (1 << 9)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                EsdOrStop = (value & (1 << 10)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                EsdOrInhibit = (value & (1 << 11)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                EsdOrTorque = (value & (1 << 12)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                CloseSpeedControl = (value & (1 << 13)) != 0 ? EnabledState.Enabled : EnabledState.Disabled,
                OpenSpeedControl = (value & (1 << 14)) != 0 ? EnabledState.Enabled : EnabledState.Disabled
            };
        }

        public override string ToString()
        {
            return $"Register 12 Flags (0x{ToRegisterValue():X4}):\n" +
                   $"  Torque Backseat: {TorqueBackseat}\n" +
                   $"  Torque Retry: {TorqueRetry}\n" +
                   $"  Remote Display: {RemoteDisplay}\n" +
                   $"  LEDs: {Leds}\n" +
                   $"  Open Inhibit: {OpenInhibit}\n" +
                   $"  Close Inhibit: {CloseInhibit}\n" +
                   $"  Local ESD: {LocalEsd}\n" +
                   $"  ESD O-R Thermal: {EsdOrThermal}\n" +
                   $"  ESD O-R Local: {EsdOrLocal}\n" +
                   $"  ESD O-R Stop: {EsdOrStop}\n" +
                   $"  ESD O-R Inhibit: {EsdOrInhibit}\n" +
                   $"  ESD O-R Torque: {EsdOrTorque}\n" +
                   $"  Close Speed Control: {CloseSpeedControl}\n" +
                   $"  Open Speed Control: {OpenSpeedControl}";
        }
    }

    // Configuration for a single actuator device
    public class ActuatorConfiguration
    {
        public byte SlaveId { get; set; }
        public ushort CloseTorque { get; set; }
        public ushort OpenTorque { get; set; }
        public ushort MinPosition { get; set; }
        public ushort MaxPosition { get; set; }
        public Register11BitFlags BitFlags { get; set; }
        public Register12BitFlags Register12Flags { get; set; }

        [JsonIgnore]
        public ushort TorqueLimit
        {
            get => CloseTorque;
            set { CloseTorque = value; OpenTorque = value; }
        }

        [JsonIgnore]
        public string DeviceName { get; set; }

        public ActuatorConfiguration()
        {
            BitFlags = new Register11BitFlags();
            Register12Flags = new Register12BitFlags();
        }

        public ActuatorConfiguration(byte slaveId)
        {
            SlaveId = slaveId;
            CloseTorque = 50;
            OpenTorque = 50;
            MinPosition = 0;
            MaxPosition = 4095;
            BitFlags = new Register11BitFlags();
            Register12Flags = new Register12BitFlags();
        }

        public override string ToString()
        {
            return $"SlaveId: {SlaveId}, " +
                   $"Close Torque: {CloseTorque}, Open Torque: {OpenTorque}, " +
                   $"Range: [{MinPosition}-{MaxPosition}]";
        }
    }

    // System configuration containing multiple actuators and connection settings
    public class SystemConfiguration
    {
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public int Parity { get; set; }
        public int StopBits { get; set; }
        public List<ActuatorConfiguration> Actuators { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public SystemConfiguration()
        {
            Actuators = new List<ActuatorConfiguration>();
            BaudRate = 9600;
            Parity = 0;
            StopBits = 1;
            CreatedDate = DateTime.Now;
            LastModifiedDate = DateTime.Now;
        }

        // Add an actuator configuration
        public void AddActuator(ActuatorConfiguration config)
        {
            if (Actuators.Any(a => a.SlaveId == config.SlaveId))
            {
                throw new ArgumentException($"Actuator with SlaveId {config.SlaveId} already exists");
            }
            Actuators.Add(config);
            LastModifiedDate = DateTime.Now;
        }

        // Remove an actuator configuration
        public bool RemoveActuator(byte slaveId)
        {
            var removed = Actuators.RemoveAll(a => a.SlaveId == slaveId) > 0;
            if (removed)
            {
                LastModifiedDate = DateTime.Now;
            }
            return removed;
        }

        // Get actuator configuration by slave ID
        public ActuatorConfiguration GetActuator(byte slaveId)
        {
            return Actuators.FirstOrDefault(a => a.SlaveId == slaveId);
        }
    }

    // Handles saving and loading configuration files
    public class ConfigurationManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Save system configuration to a JSON file
        public static void SaveConfiguration(SystemConfiguration config, string filePath)
        {
            try
            {
                config.LastModifiedDate = DateTime.Now;
                var json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(filePath, json);
                Console.WriteLine($"Configuration saved to {filePath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save configuration: {ex.Message}", ex);
            }
        }

        // Load system configuration from a JSON file
        public static SystemConfiguration LoadConfiguration(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Configuration file not found: {filePath}");
                }

                var json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<SystemConfiguration>(json, JsonOptions);
                Console.WriteLine($"Configuration loaded from {filePath}");
                return config;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load configuration: {ex.Message}", ex);
            }
        }

        // Apply a system configuration to all connected devices
        public static void ApplyConfiguration(SystemConfiguration config, IActuatorMaster master)
        {
            try
            {
                Console.WriteLine("Applying configuration...");

                foreach (var actuatorConfig in config.Actuators)
                {
                    var device = new ActuatorDevice(master, actuatorConfig.SlaveId);
                    device.ApplyConfiguration(actuatorConfig);
                    System.Threading.Thread.Sleep(100);
                }

                Console.WriteLine("Configuration applied to all devices successfully");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to apply configuration: {ex.Message}", ex);
            }
        }

        // Read current configuration from all devices
        public static SystemConfiguration ReadConfigurationFromDevices(IActuatorMaster master,
                                                                        List<byte> slaveIds)
        {
            try
            {
                var config = new SystemConfiguration();

                foreach (var slaveId in slaveIds)
                {
                    var device = new ActuatorDevice(master, slaveId);
                    var actuatorConfig = device.ReadConfiguration();
                    config.AddActuator(actuatorConfig);
                    System.Threading.Thread.Sleep(100);
                }

                Console.WriteLine($"Configuration read from {slaveIds.Count} devices");
                return config;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read configuration from devices: {ex.Message}", ex);
            }
        }

        // Create a default configuration template
        public static SystemConfiguration CreateDefaultConfiguration()
        {
            var config = new SystemConfiguration
            {
                ComPort = "COM3",
                BaudRate = 9600,
                Parity = 0,
                StopBits = 1
            };

            config.AddActuator(new ActuatorConfiguration(1)
            {
                CloseTorque = 50,
                OpenTorque = 50,
                MinPosition = 0,
                MaxPosition = 4095
            });

            return config;
        }

        // Export configuration to CSV format
        public static void ExportToCsv(SystemConfiguration config, string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("SlaveId,CloseTorque,OpenTorque,MinPosition,MaxPosition");

                    foreach (var actuator in config.Actuators)
                    {
                        writer.WriteLine($"{actuator.SlaveId},{actuator.CloseTorque},{actuator.OpenTorque}," +
                                       $"{actuator.MinPosition},{actuator.MaxPosition}");
                    }
                }
                Console.WriteLine($"Configuration exported to CSV: {filePath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to export to CSV: {ex.Message}", ex);
            }
        }
    }
}
