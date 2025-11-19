using System;

namespace ModbusActuatorControl
{
    // Single class to handle all device status (read-only registers 0-4) and commands (register 10)
    public class DeviceStatus
    {
        // Register 0 - Alarm flags
        public bool StallAlarm { get; set; }
        public bool ValveDriftAlarm { get; set; }
        public bool EsdActiveAlarm { get; set; }
        public bool MotorThermalAlarm { get; set; }
        public bool LossOfPowerAlarm { get; set; }
        public bool LossOfSignalAlarm { get; set; }
        public bool LowOilAlarm { get; set; }
        public bool UnitAlarm1 { get; set; }

        // Register 1
        public bool UnitAlarm2 { get; set; }

        // Register 2
        public bool SphExceeded { get; set; }
        public bool UnitAlert { get; set; }

        // Register 3 - Operating status
        public bool LimitSwitchOpen { get; set; }
        public bool LimitSwitchClose { get; set; }
        public bool TorqueSwitchOpen { get; set; }
        public bool TorqueSwitchClose { get; set; }
        public bool ValveOpening { get; set; }
        public bool ValveClosing { get; set; }
        public bool LocalMode { get; set; }
        public bool RemoteMode { get; set; }
        public bool StopMode { get; set; }
        public bool SetupMode { get; set; }
        public bool HandwheelPulledOut { get; set; }

        // Register 4 - Relay and DI status
        public bool Relay1Status { get; set; }
        public bool Relay2Status { get; set; }
        public bool Relay3Status { get; set; }
        public bool Relay4Status { get; set; }
        public bool Relay5MonitorStatus { get; set; }
        public bool Relay6Status { get; set; }
        public bool Relay7Status { get; set; }
        public bool Relay8Status { get; set; }
        public bool Relay9Status { get; set; }
        public bool Di1OpenStatus { get; set; }
        public bool Di2CloseStatus { get; set; }
        public bool Di3StopStatus { get; set; }
        public bool Di4EsdStatus { get; set; }
        public bool Di5PstStatus { get; set; }

        // Register 10 - Command flags (read/write)
        public bool HostOpenCmd { get; set; }
        public bool HostCloseCmd { get; set; }
        public bool HostStopCmd { get; set; }
        public bool HostEsdCmd { get; set; }
        public bool HostPstCmd { get; set; }
        public bool SoftSetupCmd { get; set; }

        // Register 24 - Valve Torque (read-only, 0-4095 representing 0.024% each)
        public ushort ValveTorque { get; set; }

        // Registers 25-28 - Analog I/O (read-only, 0-4095)
        public ushort AnalogInput1 { get; set; }
        public ushort AnalogInput2 { get; set; }
        public ushort AnalogOutput1 { get; set; }
        public ushort AnalogOutput2 { get; set; }

        // Register 29 - PST Result (read-only, 0-3: 0=Never Run, 1=In Progress, 2=Passed, 3=Failed)
        public byte PstResult { get; set; }

        // Read all status registers from device
        public void ReadFromDevice(IActuatorMaster master, byte slaveId)
        {
            var regs = master.ReadHoldingRegisters(slaveId, 0, 5);

            // Register 0
            StallAlarm = (regs[0] & (1 << 0)) != 0;
            ValveDriftAlarm = (regs[0] & (1 << 1)) != 0;
            EsdActiveAlarm = (regs[0] & (1 << 2)) != 0;
            MotorThermalAlarm = (regs[0] & (1 << 3)) != 0;
            LossOfPowerAlarm = (regs[0] & (1 << 4)) != 0;
            LossOfSignalAlarm = (regs[0] & (1 << 13)) != 0;
            LowOilAlarm = (regs[0] & (1 << 14)) != 0;
            UnitAlarm1 = (regs[0] & (1 << 15)) != 0;

            // Register 1
            UnitAlarm2 = (regs[1] & (1 << 15)) != 0;

            // Register 2
            SphExceeded = (regs[2] & (1 << 0)) != 0;
            UnitAlert = (regs[2] & (1 << 15)) != 0;

            // Register 3
            LimitSwitchOpen = (regs[3] & (1 << 0)) != 0;
            LimitSwitchClose = (regs[3] & (1 << 1)) != 0;
            TorqueSwitchOpen = (regs[3] & (1 << 2)) != 0;
            TorqueSwitchClose = (regs[3] & (1 << 3)) != 0;
            ValveOpening = (regs[3] & (1 << 4)) != 0;
            ValveClosing = (regs[3] & (1 << 5)) != 0;
            LocalMode = (regs[3] & (1 << 6)) != 0;
            RemoteMode = (regs[3] & (1 << 7)) != 0;
            StopMode = (regs[3] & (1 << 8)) != 0;
            SetupMode = (regs[3] & (1 << 9)) != 0;
            HandwheelPulledOut = (regs[3] & (1 << 10)) != 0;

            // Register 4
            Relay1Status = (regs[4] & (1 << 0)) != 0;
            Relay2Status = (regs[4] & (1 << 1)) != 0;
            Relay3Status = (regs[4] & (1 << 2)) != 0;
            Relay4Status = (regs[4] & (1 << 3)) != 0;
            Relay5MonitorStatus = (regs[4] & (1 << 4)) != 0;
            Relay6Status = (regs[4] & (1 << 5)) != 0;
            Relay7Status = (regs[4] & (1 << 6)) != 0;
            Relay8Status = (regs[4] & (1 << 7)) != 0;
            Relay9Status = (regs[4] & (1 << 8)) != 0;
            Di1OpenStatus = (regs[4] & (1 << 9)) != 0;
            Di2CloseStatus = (regs[4] & (1 << 10)) != 0;
            Di3StopStatus = (regs[4] & (1 << 11)) != 0;
            Di4EsdStatus = (regs[4] & (1 << 12)) != 0;
            Di5PstStatus = (regs[4] & (1 << 13)) != 0;

            // Register 10
            var reg10 = master.ReadHoldingRegisters(slaveId, 10, 1)[0];
            HostOpenCmd = (reg10 & (1 << 0)) != 0;
            HostCloseCmd = (reg10 & (1 << 1)) != 0;
            HostStopCmd = (reg10 & (1 << 2)) != 0;
            HostEsdCmd = (reg10 & (1 << 3)) != 0;
            HostPstCmd = (reg10 & (1 << 4)) != 0;
            SoftSetupCmd = (reg10 & (1 << 15)) != 0;

            // Register 24 - Valve Torque
            ValveTorque = master.ReadHoldingRegisters(slaveId, 24, 1)[0];

            // Registers 25-28 - Analog I/O
            var analogRegs = master.ReadHoldingRegisters(slaveId, 25, 4);
            AnalogInput1 = analogRegs[0];
            AnalogInput2 = analogRegs[1];
            AnalogOutput1 = analogRegs[2];
            AnalogOutput2 = analogRegs[3];

            // Register 29 - PST Result
            PstResult = (byte)master.ReadHoldingRegisters(slaveId, 29, 1)[0];
        }

        // Write command flags (Register 10)
        public void WriteCommandsToDevice(IActuatorMaster master, byte slaveId)
        {
            ushort reg10 = 0;
            if (HostOpenCmd) reg10 |= (1 << 0);
            if (HostCloseCmd) reg10 |= (1 << 1);
            if (HostStopCmd) reg10 |= (1 << 2);
            if (HostEsdCmd) reg10 |= (1 << 3);
            if (HostPstCmd) reg10 |= (1 << 4);
            if (SoftSetupCmd) reg10 |= (1 << 15);
            master.WriteSingleRegister(slaveId, 10, reg10);
        }

        public bool HasAnyAlarm()
        {
            return StallAlarm || ValveDriftAlarm || EsdActiveAlarm || MotorThermalAlarm ||
                   LossOfPowerAlarm || LossOfSignalAlarm || LowOilAlarm || UnitAlarm1 ||
                   UnitAlarm2 || UnitAlert;
        }

        public bool IsMoving()
        {
            return ValveOpening || ValveClosing;
        }
    }
}
