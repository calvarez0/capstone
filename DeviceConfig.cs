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

        // Register 113 (LSA and LSB)
        public byte LSA { get; set; } = 25; // Range 1-99, units = %
        public byte LSB { get; set; } = 75; // Range 1-99, units = %

        // Register 114 (Open Speed Control)
        public byte OpenSpeedControlStart { get; set; } = 70; // Range 5-95, must be multiple of 5, units = %
        public byte OpenSpeedControlRatio { get; set; } = 50; // Range 5-95, must be multiple of 5, units = %

        // Register 115 (Close Speed Control)
        public byte CloseSpeedControlStart { get; set; } = 30; // Range 5-95, must be multiple of 5, units = %
        public byte CloseSpeedControlRatio { get; set; } = 50; // Range 5-95, must be multiple of 5, units = %

        // Registers 500-507 (Calibration values - 0.024% units, range 0-4095)
        public ushort AnalogInput1ZeroCalibration { get; set; } = 0;
        public ushort AnalogInput1SpanCalibration { get; set; } = 4095;
        public ushort AnalogInput2ZeroCalibration { get; set; } = 0;
        public ushort AnalogInput2SpanCalibration { get; set; } = 4095;
        public ushort AnalogOutput1ZeroCalibration { get; set; } = 0;
        public ushort AnalogOutput1SpanCalibration { get; set; } = 4095;
        public ushort AnalogOutput2ZeroCalibration { get; set; } = 0;
        public ushort AnalogOutput2SpanCalibration { get; set; } = 4095;

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
        public void ReadFromDevice(IActuatorMaster master, byte slaveId, ushort productIdentifier = 0x8000)
        {
            // Read Register 11 (conditionally based on product)
            if (ProductCapabilities.IsRegisterAvailable(productIdentifier, 11))
            {
                var reg11 = master.ReadHoldingRegisters(slaveId, 11, 1)[0];
                if (ProductCapabilities.IsBitAvailable(productIdentifier, 11, 0))
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
            }

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

            // Registers 113-115 (LSA/LSB and Speed Control)
            var regs113_115 = master.ReadHoldingRegisters(slaveId, 113, 3);
            LSA = (byte)((regs113_115[0] >> 8) & 0xFF); // Reg 113 UH
            LSB = (byte)(regs113_115[0] & 0xFF); // Reg 113 LH
            OpenSpeedControlStart = (byte)((regs113_115[1] >> 8) & 0xFF); // Reg 114 UH
            OpenSpeedControlRatio = (byte)(regs113_115[1] & 0xFF); // Reg 114 LH
            CloseSpeedControlStart = (byte)((regs113_115[2] >> 8) & 0xFF); // Reg 115 UH
            CloseSpeedControlRatio = (byte)(regs113_115[2] & 0xFF); // Reg 115 LH

            // Registers 500-507 (Calibration values)
            var calibrationRegs = master.ReadHoldingRegisters(slaveId, 500, 8);
            AnalogInput1ZeroCalibration = calibrationRegs[0];
            AnalogInput1SpanCalibration = calibrationRegs[1];
            AnalogInput2ZeroCalibration = calibrationRegs[2];
            AnalogInput2SpanCalibration = calibrationRegs[3];
            AnalogOutput1ZeroCalibration = calibrationRegs[4];
            AnalogOutput1SpanCalibration = calibrationRegs[5];
            AnalogOutput2ZeroCalibration = calibrationRegs[6];
            AnalogOutput2SpanCalibration = calibrationRegs[7];
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

            // Write Registers 113-115 (LSA/LSB and Speed Control) with validation
            byte validatedLSA = ValidateLSA(LSA);
            byte validatedLSB = ValidateLSB(LSB);
            byte validatedOpenStart = ValidateSpeedControl(OpenSpeedControlStart);
            byte validatedOpenRatio = ValidateSpeedControl(OpenSpeedControlRatio);
            byte validatedCloseStart = ValidateSpeedControl(CloseSpeedControlStart);
            byte validatedCloseRatio = ValidateSpeedControl(CloseSpeedControlRatio);

            master.WriteSingleRegister(slaveId, 113, (ushort)((validatedLSA << 8) | validatedLSB));
            master.WriteSingleRegister(slaveId, 114, (ushort)((validatedOpenStart << 8) | validatedOpenRatio));
            master.WriteSingleRegister(slaveId, 115, (ushort)((validatedCloseStart << 8) | validatedCloseRatio));
        }

        // Validation methods
        private byte ValidateLSA(byte value)
        {
            // Range 1-99
            if (value < 1) return 1;
            if (value > 99) return 99;
            return value;
        }

        private byte ValidateLSB(byte value)
        {
            // Range 1-99
            if (value < 1) return 1;
            if (value > 99) return 99;
            return value;
        }

        private byte ValidateSpeedControl(byte value)
        {
            // Range 5-95, must be multiple of 5
            if (value < 5) return 5;
            if (value > 95) return 95;

            // Round to nearest multiple of 5
            int remainder = value % 5;
            if (remainder != 0)
            {
                if (remainder < 3)
                    value = (byte)(value - remainder);
                else
                    value = (byte)(value + (5 - remainder));
            }

            return value;
        }

        // Write calibration registers to device (500-507)
        public void WriteCalibrationToDevice(IActuatorMaster master, byte slaveId)
        {
            // Validate calibration values (0-4095)
            ushort validatedAI1Zero = ValidateCalibration(AnalogInput1ZeroCalibration);
            ushort validatedAI1Span = ValidateCalibration(AnalogInput1SpanCalibration);
            ushort validatedAI2Zero = ValidateCalibration(AnalogInput2ZeroCalibration);
            ushort validatedAI2Span = ValidateCalibration(AnalogInput2SpanCalibration);
            ushort validatedAO1Zero = ValidateCalibration(AnalogOutput1ZeroCalibration);
            ushort validatedAO1Span = ValidateCalibration(AnalogOutput1SpanCalibration);
            ushort validatedAO2Zero = ValidateCalibration(AnalogOutput2ZeroCalibration);
            ushort validatedAO2Span = ValidateCalibration(AnalogOutput2SpanCalibration);

            // Write calibration registers 500-507
            master.WriteSingleRegister(slaveId, 500, validatedAI1Zero);
            master.WriteSingleRegister(slaveId, 501, validatedAI1Span);
            master.WriteSingleRegister(slaveId, 502, validatedAI2Zero);
            master.WriteSingleRegister(slaveId, 503, validatedAI2Span);
            master.WriteSingleRegister(slaveId, 504, validatedAO1Zero);
            master.WriteSingleRegister(slaveId, 505, validatedAO1Span);
            master.WriteSingleRegister(slaveId, 506, validatedAO2Zero);
            master.WriteSingleRegister(slaveId, 507, validatedAO2Span);
        }

        private ushort ValidateCalibration(ushort value)
        {
            // Range 0-4095
            if (value > 4095) return 4095;
            return value;
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
            // Create a filtered version for serialization
            var filteredConfig = new
            {
                ComPort,
                BaudRate,
                Parity,
                StopBits,
                Actuators = Actuators.Select(a => CreateFilteredActuatorConfig(a)).ToList()
            };

            var json = JsonSerializer.Serialize(filteredConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private object CreateFilteredActuatorConfig(ActuatorConfig actuator)
        {
            var productId = actuator.ProductIdentifier;
            var config = actuator.Config;

            // Build a dynamic object with only available properties
            var filteredProps = new Dictionary<string, object>
            {
                ["SlaveId"] = actuator.SlaveId,
                ["DeviceName"] = actuator.DeviceName,
                ["ProductIdentifier"] = actuator.ProductIdentifier
            };

            // Add torque if register 112 is available
            if (ProductCapabilities.IsRegisterAvailable(productId, 112))
            {
                filteredProps["CloseTorque"] = actuator.CloseTorque;
                filteredProps["OpenTorque"] = actuator.OpenTorque;
            }

            // Add PST result if register 29 is available
            if (ProductCapabilities.IsRegisterAvailable(productId, 29))
            {
                filteredProps["PstResult"] = actuator.PstResult;
            }

            // Create nested Config object
            var configProps = new Dictionary<string, object>();

            // Register 11 bits - add only if available
            if (ProductCapabilities.IsBitAvailable(productId, 11, 0))
                configProps[nameof(config.EhoType)] = config.EhoType.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 1))
                configProps[nameof(config.LocalInputFunction)] = config.LocalInputFunction.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 2))
                configProps[nameof(config.RemoteInputFunction)] = config.RemoteInputFunction.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 3))
                configProps[nameof(config.RemoteEsdEnabled)] = config.RemoteEsdEnabled.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 4))
                configProps[nameof(config.LossCommEnabled)] = config.LossCommEnabled.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 5))
                configProps[nameof(config.Ai1Polarity)] = config.Ai1Polarity.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 6))
                configProps[nameof(config.Ai2Polarity)] = config.Ai2Polarity.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 7))
                configProps[nameof(config.Ao1Polarity)] = config.Ao1Polarity.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 8))
                configProps[nameof(config.Ao2Polarity)] = config.Ao2Polarity.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 9))
                configProps[nameof(config.Di1OpenTrigger)] = config.Di1OpenTrigger.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 10))
                configProps[nameof(config.Di2CloseTrigger)] = config.Di2CloseTrigger.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 11))
                configProps[nameof(config.Di3StopTrigger)] = config.Di3StopTrigger.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 12))
                configProps[nameof(config.Di4EsdTrigger)] = config.Di4EsdTrigger.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 13))
                configProps[nameof(config.Di5PstTrigger)] = config.Di5PstTrigger.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 14))
                configProps[nameof(config.CloseDirection)] = config.CloseDirection.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 11, 15))
                configProps[nameof(config.Seat)] = config.Seat.ToString();

            // Register 12 bits
            if (ProductCapabilities.IsBitAvailable(productId, 12, 0))
                configProps[nameof(config.TorqueBackseat)] = config.TorqueBackseat.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 1))
                configProps[nameof(config.TorqueRetry)] = config.TorqueRetry.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 2))
                configProps[nameof(config.RemoteDisplay)] = config.RemoteDisplay.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 3))
                configProps[nameof(config.Leds)] = config.Leds.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 4))
                configProps[nameof(config.OpenInhibit)] = config.OpenInhibit.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 5))
                configProps[nameof(config.CloseInhibit)] = config.CloseInhibit.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 6))
                configProps[nameof(config.LocalEsd)] = config.LocalEsd.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 7))
                configProps[nameof(config.EsdOrThermal)] = config.EsdOrThermal.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 8))
                configProps[nameof(config.EsdOrLocal)] = config.EsdOrLocal.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 9))
                configProps[nameof(config.EsdOrStop)] = config.EsdOrStop.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 10))
                configProps[nameof(config.EsdOrInhibit)] = config.EsdOrInhibit.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 11))
                configProps[nameof(config.EsdOrTorque)] = config.EsdOrTorque.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 12))
                configProps[nameof(config.CloseSpeedControl)] = config.CloseSpeedControl.ToString();
            if (ProductCapabilities.IsBitAvailable(productId, 12, 13))
                configProps[nameof(config.OpenSpeedControl)] = config.OpenSpeedControl.ToString();

            // Control Configuration (Registers 101-102)
            if (ProductCapabilities.IsRegisterAvailable(productId, 101) || ProductCapabilities.IsRegisterAvailable(productId, 102))
            {
                configProps[nameof(config.ControlMode)] = config.ControlMode.ToString();
                configProps[nameof(config.ModulationDelay)] = config.ModulationDelay;
                configProps[nameof(config.Deadband)] = config.Deadband;
                configProps[nameof(config.NetworkAdapter)] = config.NetworkAdapter.ToString();
            }

            // Relays (Registers 103-106)
            if (ProductCapabilities.IsRegisterAvailable(productId, 103))
            {
                // Convert relay tuples to serializable objects
                var relayObjects = config.Relays.Select((relay, index) => new
                {
                    Index = index,
                    Trigger = relay.Item1.ToString(),
                    Mode = relay.Item2.ToString(),
                    ContactType = relay.Item3.ToString()
                }).ToList();
                configProps["Relays"] = relayObjects;
            }

            // Additional Functions (Registers 107-110)
            if (ProductCapabilities.IsRegisterAvailable(productId, 107))
            {
                configProps[nameof(config.FailsafeFunction)] = config.FailsafeFunction.ToString();
                configProps[nameof(config.FailsafeGoToPosition)] = config.FailsafeGoToPosition;
            }
            if (ProductCapabilities.IsRegisterAvailable(productId, 108))
            {
                configProps[nameof(config.EsdFunction)] = config.EsdFunction.ToString();
            }
            if (ProductCapabilities.IsRegisterAvailable(productId, 109))
            {
                configProps[nameof(config.EsdDelay)] = config.EsdDelay;
                configProps[nameof(config.LossCommFunction)] = config.LossCommFunction.ToString();
            }
            if (ProductCapabilities.IsRegisterAvailable(productId, 110))
            {
                configProps[nameof(config.LossCommDelay)] = config.LossCommDelay;
                configProps[nameof(config.NetworkBaudRate)] = config.NetworkBaudRate.ToString();
            }

            // Network settings (Register 111)
            configProps[nameof(config.NetworkResponseDelay)] = config.NetworkResponseDelay;
            configProps[nameof(config.NetworkCommParity)] = config.NetworkCommParity.ToString();

            // LSA/LSB and Speed Control (Registers 113-115)
            if (ProductCapabilities.IsRegisterAvailable(productId, 113))
            {
                configProps[nameof(config.LSA)] = config.LSA;
                configProps[nameof(config.LSB)] = config.LSB;
            }
            if (ProductCapabilities.IsRegisterAvailable(productId, 114))
            {
                configProps[nameof(config.OpenSpeedControlStart)] = config.OpenSpeedControlStart;
                configProps[nameof(config.OpenSpeedControlRatio)] = config.OpenSpeedControlRatio;
            }
            if (ProductCapabilities.IsRegisterAvailable(productId, 115))
            {
                configProps[nameof(config.CloseSpeedControlStart)] = config.CloseSpeedControlStart;
                configProps[nameof(config.CloseSpeedControlRatio)] = config.CloseSpeedControlRatio;
            }

            // Calibration values (Registers 500-507)
            if (ProductCapabilities.IsRegisterAvailable(productId, 500))
                configProps[nameof(config.AnalogInput1ZeroCalibration)] = config.AnalogInput1ZeroCalibration;
            if (ProductCapabilities.IsRegisterAvailable(productId, 501))
                configProps[nameof(config.AnalogInput1SpanCalibration)] = config.AnalogInput1SpanCalibration;
            if (ProductCapabilities.IsRegisterAvailable(productId, 502))
                configProps[nameof(config.AnalogInput2ZeroCalibration)] = config.AnalogInput2ZeroCalibration;
            if (ProductCapabilities.IsRegisterAvailable(productId, 503))
                configProps[nameof(config.AnalogInput2SpanCalibration)] = config.AnalogInput2SpanCalibration;
            if (ProductCapabilities.IsRegisterAvailable(productId, 504))
                configProps[nameof(config.AnalogOutput1ZeroCalibration)] = config.AnalogOutput1ZeroCalibration;
            if (ProductCapabilities.IsRegisterAvailable(productId, 505))
                configProps[nameof(config.AnalogOutput1SpanCalibration)] = config.AnalogOutput1SpanCalibration;
            if (ProductCapabilities.IsRegisterAvailable(productId, 506))
                configProps[nameof(config.AnalogOutput2ZeroCalibration)] = config.AnalogOutput2ZeroCalibration;
            if (ProductCapabilities.IsRegisterAvailable(productId, 507))
                configProps[nameof(config.AnalogOutput2SpanCalibration)] = config.AnalogOutput2SpanCalibration;

            filteredProps["Config"] = configProps;

            return filteredProps;
        }
    }

    // Per-actuator configuration for serialization
    public class ActuatorConfig
    {
        public byte SlaveId { get; set; }
        public string DeviceName { get; set; } = "";
        public ushort ProductIdentifier { get; set; } = 0x8000; // 0x8000=S7X, 0x8001=EHO, 0x8002=Nova
        public ushort CloseTorque { get; set; } = 50;
        public ushort OpenTorque { get; set; } = 50;
        public byte PstResult { get; set; } = 0; // 0=Never Run, 1=In Progress, 2=Passed, 3=Failed
        public DeviceConfig Config { get; set; } = new DeviceConfig();
    }
}
