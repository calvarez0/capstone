using System;
using System.Text.Json.Serialization;

namespace WPF_GUI.Models
{
    public class ActuatorConfiguration
    {
        public string DeviceName { get; set; } = "S7X-001";
        public int ModbusAddress { get; set; } = 254;
        public double OpenPosition { get; set; } = 100;
        public double ClosePosition { get; set; } = 0;
        public int SpeedSetting { get; set; } = 50;
        public int TorqueLimit { get; set; } = 80;
        public double PositionDeadband { get; set; } = 2.0;
        public string FailsafeMode { get; set; } = "Close";
        public double AnalogOutputMin { get; set; } = 4.0;
        public double AnalogOutputMax { get; set; } = 20.0;

        public ActuatorConfiguration Clone()
        {
            return new ActuatorConfiguration
            {
                DeviceName = this.DeviceName,
                ModbusAddress = this.ModbusAddress,
                OpenPosition = this.OpenPosition,
                ClosePosition = this.ClosePosition,
                SpeedSetting = this.SpeedSetting,
                TorqueLimit = this.TorqueLimit,
                PositionDeadband = this.PositionDeadband,
                FailsafeMode = this.FailsafeMode,
                AnalogOutputMin = this.AnalogOutputMin,
                AnalogOutputMax = this.AnalogOutputMax
            };
        }
    }
}
