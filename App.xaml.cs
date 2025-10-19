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
        public static SimulationService SimulationService { get; private set; }
        public static DeviceState DeviceState { get; private set; }
        public static ActuatorConfiguration CurrentConfiguration { get; set; }

        public static bool IsSimulationMode { get; private set; } = true; // Default to simulation

        public static event EventHandler<bool> ModeChanged;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize shared services and state
            ModbusService = new ModbusService();
            DeviceState = new DeviceState();
            CurrentConfiguration = new ActuatorConfiguration();
            SimulationService = new SimulationService(DeviceState, CurrentConfiguration);

            // Start in simulation mode
            SimulationService.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up resources
            SimulationService?.Stop();
            ModbusService?.Dispose();
            base.OnExit(e);
        }

        public static void SetMode(bool isSimulation)
        {
            if (IsSimulationMode == isSimulation)
                return; // No change

            IsSimulationMode = isSimulation;

            if (isSimulation)
            {
                // Switch to simulation mode
                ModbusService.ClosePort();
                SimulationService.Start();
            }
            else
            {
                // Switch to production mode
                SimulationService.Stop();
                // Note: User must manually connect in production mode
            }

            // Notify all pages that mode has changed
            ModeChanged?.Invoke(null, isSimulation);
        }
    }
}
