using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModbusActuatorControl
{
    // Configuration for a single actuator device
    public class ActuatorConfiguration
    {
        public byte SlaveId { get; set; }
        public ushort CloseTorque { get; set; }
        public ushort OpenTorque { get; set; }
        public ushort MinPosition { get; set; }
        public ushort MaxPosition { get; set; }

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
        }

        public ActuatorConfiguration(byte slaveId)
        {
            SlaveId = slaveId;
            CloseTorque = 50;
            OpenTorque = 50;
            MinPosition = 0;
            MaxPosition = 4095;
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
