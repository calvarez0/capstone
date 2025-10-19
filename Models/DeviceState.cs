using System;

namespace WPF_GUI.Models
{
    public class DeviceState
    {
        // Position and Torque
        public double Position { get; set; }
        public double Torque { get; set; }

        // Status
        public string CurrentStatus { get; set; } = "Stopped";

        // Status Bits
        public bool PowerOK { get; set; }
        public bool Communication { get; set; }
        public bool Calibrated { get; set; }
        public bool Moving { get; set; }
        public bool OpenLimit { get; set; }
        public bool CloseLimit { get; set; }

        // Alarms
        public string[] ActiveAlarms { get; set; } = new string[0];

        public DeviceState()
        {
            Position = 0;
            Torque = 0;
            PowerOK = false;
            Communication = false;
            Calibrated = false;
            Moving = false;
            OpenLimit = false;
            CloseLimit = false;
        }
    }
}
