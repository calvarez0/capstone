using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModbusActuatorControl
{
    // All enums in one place
    public enum EhoType { DoubleAction = 0, SpringReturn = 1 }
    public enum InputFunction { Maintained = 0, Momentary = 1 }
    public enum EnabledState { Disabled = 0, Enabled = 1 }
    public enum Polarity { Normal = 0, Reversed = 1 }
    public enum TriggerType { NormallyOpen = 0, NormallyClose = 1 }
    public enum CloseDirection { Clockwise = 0, CounterClockwise = 1 }
    public enum SeatMode { Position = 0, Torque = 1 }
    public enum LedColorScheme { CloseGreenOpenRed = 0, CloseRedOpenGreen = 1 }
    public enum ControlMode { TwoWireDiscrete = 0, ThreeWireDiscrete = 1, FourWireDiscrete = 2, Analog4_20mA = 3, Analog0_10V = 4, Analog2_10V = 5, Analog0_5V = 6, Analog1_5V = 7, Network = 8 }
    public enum NetworkAdapter { None = 0, ModbusBUS = 1, ModbusRepeater = 2, ModbusTCP = 3, DeviceNet = 4, EthernetIP = 5, Profibus = 6, ProfiNet = 7, HART = 8, HART_IP = 9, FoundationFieldbus = 10 }
    public enum RelayTrigger { LSO = 0, LSC = 1, LSA = 2, LSB = 3, Opening = 4, Closing = 5, OpenTorque = 6, CloseTorque = 7, Local = 8, Stop = 9, Remote = 10, ValveDrift = 16, LostPower = 17, LostPhase = 18, MotorOverload = 19, OpenInhibit = 20, CloseInhibit = 21, LocalESD = 22, AnyESD = 23, LostAnalog = 24, Generic = 25, Moving = 26, ValveStall = 27, MonitorRelay = 28, PSTInProcess = 29 }
    public enum RelayMode { Continuous = 0, Flashing = 1 }
    public enum RelayContactType { NormallyClosed = 0, NormallyOpen = 1 }
    public enum FunctionAction { StayPut = 0, GoOpen = 1, GoClose = 2, GoToPosition = 3 }
    public enum NetworkBaudRate { Baud1200 = 0, Baud2400 = 1, Baud4800 = 2, Baud9600 = 3, Baud19200 = 4, Baud38400 = 5 }
    public enum NetworkCommParity { None = 0, Odd = 1, Even = 2 }

    // Helper class for relay JSON serialization
    public class RelayConfig
    {
        public RelayTrigger Trigger { get; set; }
        public RelayMode Mode { get; set; }
        public RelayContactType ContactType { get; set; }

        public RelayConfig() { }

        public RelayConfig(RelayTrigger trigger, RelayMode mode, RelayContactType contact)
        {
            Trigger = trigger;
            Mode = mode;
            ContactType = contact;
        }

        public (RelayTrigger, RelayMode, RelayContactType) ToTuple()
        {
            return (Trigger, Mode, ContactType);
        }
    }

    // Single configuration class for all device registers
    public class DeviceConfig
    {
        // Register 11 bit flags
        public EhoType EhoType { get; set; }
        public InputFunction LocalInputFunction { get; set; }
        public InputFunction RemoteInputFunction { get; set; }
        public EnabledState RemoteEsdEnabled { get; set; }
        public EnabledState LossCommEnabled { get; set; }
        public Polarity Ai1Polarity { get; set; }
        public Polarity Ai2Polarity { get; set; }
        public Polarity Ao1Polarity { get; set; }
        public Polarity Ao2Polarity { get; set; }
        public TriggerType Di1OpenTrigger { get; set; }
        public TriggerType Di2CloseTrigger { get; set; }
        public TriggerType Di3StopTrigger { get; set; }
        public TriggerType Di4EsdTrigger { get; set; }
        public TriggerType Di5PstTrigger { get; set; }
        public CloseDirection CloseDirection { get; set; }
        public SeatMode Seat { get; set; }

        // Register 12 flags (bits 0-13, 14-15 unused)
        public EnabledState TorqueBackseat { get; set; }               // Bit 0
        public EnabledState TorqueRetry { get; set; }                  // Bit 1
        public EnabledState RemoteDisplay { get; set; }                // Bit 2
        public EnabledState Leds { get; set; }                         // Bit 3
        public EnabledState OpenInhibit { get; set; }                  // Bit 4
        public EnabledState CloseInhibit { get; set; }                 // Bit 5
        public EnabledState LocalEsd { get; set; }                     // Bit 6
        public EnabledState EsdOrThermal { get; set; }                 // Bit 7
        public EnabledState EsdOrLocal { get; set; }                   // Bit 8
        public EnabledState EsdOrStop { get; set; }                    // Bit 9
        public EnabledState EsdOrInhibit { get; set; }                 // Bit 10
        public EnabledState EsdOrTorque { get; set; }                  // Bit 11
        public EnabledState CloseSpeedControl { get; set; }            // Bit 12
        public EnabledState OpenSpeedControl { get; set; }             // Bit 13
        // Bits 14-15 unused

        // Register 101-102 (Control)
        public ControlMode ControlMode { get; set; }
        public byte ModulationDelay { get; set; } = 1;
        public byte Deadband { get; set; } = 20;
        public NetworkAdapter NetworkAdapter { get; set; }

        // Registers 103-111 (Relays - 9 relays stored as bytes in registers 103-107 + extra)
        [JsonIgnore]
        private List<(RelayTrigger trigger, RelayMode mode, RelayContactType contact)> _relays;

        [JsonIgnore]
        public List<(RelayTrigger trigger, RelayMode mode, RelayContactType contact)> Relays
        {
            get => _relays;
            set => _relays = value;
        }

        // For JSON serialization
        public List<RelayConfig> RelaysList
        {
            get => _relays.Select(r => new RelayConfig(r.trigger, r.mode, r.contact)).ToList();
            set => _relays = value.Select(r => r.ToTuple()).ToList();
        }

        // Registers 107-110 (Additional Functions)
        public FunctionAction FailsafeFunction { get; set; }
        public byte FailsafeGoToPosition { get; set; } = 50;
        public FunctionAction EsdFunction { get; set; }
        public byte EsdDelay { get; set; }
        public FunctionAction LossCommFunction { get; set; }
        public byte LossCommDelay { get; set; }

        // Registers 110-111 (Network)
        public NetworkBaudRate NetworkBaudRate { get; set; } = NetworkBaudRate.Baud9600;
        public byte NetworkResponseDelay { get; set; } = 8;
        public NetworkCommParity NetworkCommParity { get; set; }

        public DeviceConfig()
        {
            // Defaults for Register 11
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

            // Defaults for Register 12 (bits 0-13)
            TorqueBackseat = EnabledState.Disabled;
            TorqueRetry = EnabledState.Disabled;
            RemoteDisplay = EnabledState.Disabled;
            Leds = EnabledState.Disabled;
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

            // Defaults for Control
            ControlMode = ControlMode.TwoWireDiscrete;
            ModulationDelay = 1;
            Deadband = 20;
            NetworkAdapter = NetworkAdapter.None;

            // Defaults for Relays (9 relays)
            _relays = new List<(RelayTrigger, RelayMode, RelayContactType)>();
            for (int i = 0; i < 9; i++)
                _relays.Add((RelayTrigger.LSO, RelayMode.Continuous, RelayContactType.NormallyClosed));

            // Defaults for Additional Functions
            FailsafeFunction = FunctionAction.StayPut;
            FailsafeGoToPosition = 50;
            EsdFunction = FunctionAction.StayPut;
            EsdDelay = 0;
            LossCommFunction = FunctionAction.StayPut;
            LossCommDelay = 0;

            // Defaults for Network
            NetworkBaudRate = NetworkBaudRate.Baud9600;
            NetworkResponseDelay = 8;
            NetworkCommParity = NetworkCommParity.None;
        }

        // Read all config registers from device
        public void ReadFromDevice(IActuatorMaster master, byte slaveId)
        {
            // Read Register 11
            var reg11 = master.ReadHoldingRegisters(slaveId, 11, 1)[0];
            EhoType = (reg11 & (1 << 0)) != 0 ? EhoType.SpringReturn : EhoType.DoubleAction;
            LocalInputFunction = (reg11 & (1 << 1)) != 0 ? InputFunction.Momentary : InputFunction.Maintained;
            RemoteInputFunction = (reg11 & (1 << 2)) != 0 ? InputFunction.Momentary : InputFunction.Maintained;
            RemoteEsdEnabled = (reg11 & (1 << 3)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            LossCommEnabled = (reg11 & (1 << 4)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            Ai1Polarity = (reg11 & (1 << 5)) != 0 ? Polarity.Reversed : Polarity.Normal;
            Ai2Polarity = (reg11 & (1 << 6)) != 0 ? Polarity.Reversed : Polarity.Normal;
            Ao1Polarity = (reg11 & (1 << 7)) != 0 ? Polarity.Reversed : Polarity.Normal;
            Ao2Polarity = (reg11 & (1 << 8)) != 0 ? Polarity.Reversed : Polarity.Normal;
            Di1OpenTrigger = (reg11 & (1 << 9)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            Di2CloseTrigger = (reg11 & (1 << 10)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            Di3StopTrigger = (reg11 & (1 << 11)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            Di4EsdTrigger = (reg11 & (1 << 12)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            Di5PstTrigger = (reg11 & (1 << 13)) != 0 ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            CloseDirection = (reg11 & (1 << 14)) != 0 ? CloseDirection.CounterClockwise : CloseDirection.Clockwise;
            Seat = (reg11 & (1 << 15)) != 0 ? SeatMode.Torque : SeatMode.Position;

            // Read Register 12 (bits 0-13 used, 14-15 unused)
            var reg12 = master.ReadHoldingRegisters(slaveId, 12, 1)[0];
            TorqueBackseat = (reg12 & (1 << 0)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            TorqueRetry = (reg12 & (1 << 1)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            RemoteDisplay = (reg12 & (1 << 2)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            Leds = (reg12 & (1 << 3)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            OpenInhibit = (reg12 & (1 << 4)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            CloseInhibit = (reg12 & (1 << 5)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            LocalEsd = (reg12 & (1 << 6)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            EsdOrThermal = (reg12 & (1 << 7)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            EsdOrLocal = (reg12 & (1 << 8)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            EsdOrStop = (reg12 & (1 << 9)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            EsdOrInhibit = (reg12 & (1 << 10)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            EsdOrTorque = (reg12 & (1 << 11)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            CloseSpeedControl = (reg12 & (1 << 12)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;
            OpenSpeedControl = (reg12 & (1 << 13)) != 0 ? EnabledState.Enabled : EnabledState.Disabled;

            // Read Registers 101-102 (Control)
            var regs101_102 = master.ReadHoldingRegisters(slaveId, 101, 2);
            ControlMode = (ControlMode)((regs101_102[0] >> 8) & 0xFF);
            ModulationDelay = (byte)(regs101_102[0] & 0xFF);
            Deadband = (byte)((regs101_102[1] >> 8) & 0xFF);
            NetworkAdapter = (NetworkAdapter)(regs101_102[1] & 0xFF);

            // Read Registers 103-111 for relays and functions
            var regs103_111 = master.ReadHoldingRegisters(slaveId, 103, 9);

            // Relays (103-107 contain relay config, 9 relays total)
            Relays.Clear();
            for (int i = 0; i < 5; i++)
            {
                var reg = regs103_111[i];
                byte uh = (byte)((reg >> 8) & 0xFF);
                byte lh = (byte)(reg & 0xFF);

                if (i * 2 < 9) Relays.Add(ParseRelay(uh));
                if (i * 2 + 1 < 9) Relays.Add(ParseRelay(lh));
            }

            // Additional Functions (107-110)
            FailsafeFunction = (FunctionAction)((regs103_111[4] >> 8) & 0xFF); // Reg 107 UH
            FailsafeGoToPosition = (byte)((regs103_111[5] >> 8) & 0xFF); // Reg 108 UH
            EsdFunction = (FunctionAction)(regs103_111[5] & 0xFF); // Reg 108 LH
            EsdDelay = (byte)((regs103_111[6] >> 8) & 0xFF); // Reg 109 UH
            LossCommFunction = (FunctionAction)(regs103_111[6] & 0xFF); // Reg 109 LH
            LossCommDelay = (byte)((regs103_111[7] >> 8) & 0xFF); // Reg 110 UH

            // Network (110 LH, 111)
            NetworkBaudRate = (NetworkBaudRate)(regs103_111[7] & 0xFF); // Reg 110 LH
            NetworkResponseDelay = (byte)((regs103_111[8] >> 8) & 0xFF); // Reg 111 UH
            NetworkCommParity = (NetworkCommParity)(regs103_111[8] & 0xFF); // Reg 111 LH
        }

        // Write all config registers to device
        public void WriteToDevice(IActuatorMaster master, byte slaveId)
        {
            // Write Register 11
            ushort reg11 = 0;
            if (EhoType == EhoType.SpringReturn) reg11 |= (1 << 0);
            if (LocalInputFunction == InputFunction.Momentary) reg11 |= (1 << 1);
            if (RemoteInputFunction == InputFunction.Momentary) reg11 |= (1 << 2);
            if (RemoteEsdEnabled == EnabledState.Enabled) reg11 |= (1 << 3);
            if (LossCommEnabled == EnabledState.Enabled) reg11 |= (1 << 4);
            if (Ai1Polarity == Polarity.Reversed) reg11 |= (1 << 5);
            if (Ai2Polarity == Polarity.Reversed) reg11 |= (1 << 6);
            if (Ao1Polarity == Polarity.Reversed) reg11 |= (1 << 7);
            if (Ao2Polarity == Polarity.Reversed) reg11 |= (1 << 8);
            if (Di1OpenTrigger == TriggerType.NormallyClose) reg11 |= (1 << 9);
            if (Di2CloseTrigger == TriggerType.NormallyClose) reg11 |= (1 << 10);
            if (Di3StopTrigger == TriggerType.NormallyClose) reg11 |= (1 << 11);
            if (Di4EsdTrigger == TriggerType.NormallyClose) reg11 |= (1 << 12);
            if (Di5PstTrigger == TriggerType.NormallyClose) reg11 |= (1 << 13);
            if (CloseDirection == CloseDirection.CounterClockwise) reg11 |= (1 << 14);
            if (Seat == SeatMode.Torque) reg11 |= (1 << 15);
            master.WriteSingleRegister(slaveId, 11, reg11);

            // Write Register 12 (bits 0-13)
            ushort reg12 = 0;
            if (TorqueBackseat == EnabledState.Enabled) reg12 |= (1 << 0);
            if (TorqueRetry == EnabledState.Enabled) reg12 |= (1 << 1);
            if (RemoteDisplay == EnabledState.Enabled) reg12 |= (1 << 2);
            if (Leds == EnabledState.Enabled) reg12 |= (1 << 3);
            if (OpenInhibit == EnabledState.Enabled) reg12 |= (1 << 4);
            if (CloseInhibit == EnabledState.Enabled) reg12 |= (1 << 5);
            if (LocalEsd == EnabledState.Enabled) reg12 |= (1 << 6);
            if (EsdOrThermal == EnabledState.Enabled) reg12 |= (1 << 7);
            if (EsdOrLocal == EnabledState.Enabled) reg12 |= (1 << 8);
            if (EsdOrStop == EnabledState.Enabled) reg12 |= (1 << 9);
            if (EsdOrInhibit == EnabledState.Enabled) reg12 |= (1 << 10);
            if (EsdOrTorque == EnabledState.Enabled) reg12 |= (1 << 11);
            if (CloseSpeedControl == EnabledState.Enabled) reg12 |= (1 << 12);
            if (OpenSpeedControl == EnabledState.Enabled) reg12 |= (1 << 13);
            master.WriteSingleRegister(slaveId, 12, reg12);

            // Write Registers 101-102
            master.WriteSingleRegister(slaveId, 101, (ushort)(((byte)ControlMode << 8) | ModulationDelay));
            master.WriteSingleRegister(slaveId, 102, (ushort)((Deadband << 8) | (byte)NetworkAdapter));

            // Write Relays (103-107)
            for (int i = 0; i < 5; i++)
            {
                byte uh = (i * 2 < Relays.Count) ? EncodeRelay(Relays[i * 2]) : (byte)0;
                byte lh = (i * 2 + 1 < Relays.Count) ? EncodeRelay(Relays[i * 2 + 1]) : (byte)0;
                master.WriteSingleRegister(slaveId, (ushort)(103 + i), (ushort)((uh << 8) | lh));
            }

            // Write Additional Functions + Network (107-111)
            master.WriteSingleRegister(slaveId, 107, (ushort)((byte)FailsafeFunction << 8));
            master.WriteSingleRegister(slaveId, 108, (ushort)((FailsafeGoToPosition << 8) | (byte)EsdFunction));
            master.WriteSingleRegister(slaveId, 109, (ushort)((EsdDelay << 8) | (byte)LossCommFunction));
            master.WriteSingleRegister(slaveId, 110, (ushort)((LossCommDelay << 8) | (byte)NetworkBaudRate));
            master.WriteSingleRegister(slaveId, 111, (ushort)((NetworkResponseDelay << 8) | (byte)NetworkCommParity));
        }

        private (RelayTrigger, RelayMode, RelayContactType) ParseRelay(byte value)
        {
            var trigger = (RelayTrigger)(value & 0x3F);
            var mode = (value & (1 << 6)) != 0 ? RelayMode.Flashing : RelayMode.Continuous;
            var contact = (value & (1 << 7)) != 0 ? RelayContactType.NormallyOpen : RelayContactType.NormallyClosed;
            return (trigger, mode, contact);
        }

        private byte EncodeRelay((RelayTrigger trigger, RelayMode mode, RelayContactType contact) relay)
        {
            byte value = (byte)relay.trigger;
            if (relay.mode == RelayMode.Flashing) value |= (1 << 6);
            if (relay.contact == RelayContactType.NormallyOpen) value |= (1 << 7);
            return value;
        }
    }

    // System configuration for serialization
    public class SystemConfig
    {
        public string ComPort { get; set; } = "";
        public int BaudRate { get; set; } = 9600;
        public int Parity { get; set; } = 0;
        public int StopBits { get; set; } = 1;
        public List<ActuatorConfig> Actuators { get; set; } = new List<ActuatorConfig>();

        public static SystemConfig LoadFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SystemConfig>(json) ?? new SystemConfig();
        }

        public void SaveToFile(string path)
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
    }

    // Per-actuator configuration for serialization
    public class ActuatorConfig
    {
        public byte SlaveId { get; set; }
        public string DeviceName { get; set; } = "";
        public ushort CloseTorque { get; set; } = 50;
        public ushort OpenTorque { get; set; } = 50;
        public ushort MinPosition { get; set; } = 0;
        public ushort MaxPosition { get; set; } = 4095;
        public DeviceConfig Config { get; set; } = new DeviceConfig();
    }
}
