using System.Configuration;
using System.Data;
using System.Windows;
using WPF_GUI.Services;
using WPF_GUI.Models;

namespace WPF_GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ModbusService ModbusService { get; private set; }
        public static DeviceState DeviceState { get; private set; }
        public static ActuatorConfiguration CurrentConfiguration { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize shared services and state
            ModbusService = new ModbusService();
            DeviceState = new DeviceState();
            CurrentConfiguration = new ActuatorConfiguration();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up resources
            ModbusService?.Dispose();
            base.OnExit(e);
        }
    }
}
