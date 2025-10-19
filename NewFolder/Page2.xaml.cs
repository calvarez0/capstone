using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPF_GUI.Services;
using WPF_GUI.Models;

namespace WPF_GUI.NewFolder
{
    /// <summary>
    /// Interaction logic for Page2.xaml
    /// </summary>
    public partial class Page2 : Page
    {
        private ModbusService _modbusService;
        private DeviceState _deviceState;
        private DispatcherTimer _pollingTimer;
        private const int POLLING_INTERVAL_MS = 500; // Poll every 500ms

        public Page2()
        {
            InitializeComponent();
            _modbusService = App.ModbusService;
            _deviceState = App.DeviceState;

            // Wire up button events
            OpenCommandButton.Click += OpenCommandButton_Click;
            CloseCommandButton.Click += CloseCommandButton_Click;
            StopCommandButton.Click += StopCommandButton_Click;

            // Setup polling timer
            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromMilliseconds(POLLING_INTERVAL_MS);
            _pollingTimer.Tick += PollingTimer_Tick;

            // Subscribe to page loaded/unloaded
            this.Loaded += Page2_Loaded;
            this.Unloaded += Page2_Unloaded;
        }

        private void Page2_Loaded(object sender, RoutedEventArgs e)
        {
            // Start polling if connected
            if (_modbusService.IsConnected)
            {
                _pollingTimer.Start();
            }
        }

        private void Page2_Unloaded(object sender, RoutedEventArgs e)
        {
            // Stop polling when page is unloaded
            _pollingTimer.Stop();
        }

        private void PollingTimer_Tick(object sender, EventArgs e)
        {
            if (!_modbusService.IsConnected)
            {
                _pollingTimer.Stop();
                return;
            }

            PollDeviceStatus();
        }

        private void PollDeviceStatus()
        {
            try
            {
                // Read status registers (example addresses - adjust based on actual Modbus map)
                // Register 0: Position (0-1000 representing 0-100%)
                // Register 1: Torque (0-1000 representing 0-100%)
                // Register 2: Status Word
                ushort[] statusRegisters = _modbusService.ReadHoldingRegisters(0, 3);

                if (statusRegisters != null && statusRegisters.Length >= 3)
                {
                    // Update position (convert from 0-1000 to 0-100%)
                    _deviceState.Position = statusRegisters[0] / 10.0;

                    // Update torque (convert from 0-1000 to 0-100%)
                    _deviceState.Torque = statusRegisters[1] / 10.0;

                    // Parse status word (bit fields)
                    ushort statusWord = statusRegisters[2];
                    _deviceState.PowerOK = (statusWord & 0x0001) != 0;
                    _deviceState.Communication = (statusWord & 0x0002) != 0;
                    _deviceState.Calibrated = (statusWord & 0x0004) != 0;
                    _deviceState.Moving = (statusWord & 0x0008) != 0;
                    _deviceState.OpenLimit = (statusWord & 0x0010) != 0;
                    _deviceState.CloseLimit = (statusWord & 0x0020) != 0;

                    // Determine current status
                    if (_deviceState.Moving)
                    {
                        if (_deviceState.Position > 50)
                            _deviceState.CurrentStatus = "OPENING";
                        else
                            _deviceState.CurrentStatus = "CLOSING";
                    }
                    else if (_deviceState.OpenLimit)
                    {
                        _deviceState.CurrentStatus = "OPEN";
                    }
                    else if (_deviceState.CloseLimit)
                    {
                        _deviceState.CurrentStatus = "CLOSED";
                    }
                    else
                    {
                        _deviceState.CurrentStatus = "STOPPED";
                    }

                    // Update UI
                    UpdateUI();
                }
            }
            catch (Exception ex)
            {
                // Handle polling errors silently or log them
                System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            // Update position bar
            double positionWidth = (PositionFill.Parent as Border).ActualWidth * (_deviceState.Position / 100.0);
            PositionFill.Width = Math.Max(0, Math.Min(positionWidth, (PositionFill.Parent as Border).ActualWidth));
            PositionValueText.Text = $"{_deviceState.Position:F1}%";

            // Update torque bar
            double torqueWidth = (TorqueFill.Parent as Border).ActualWidth * (_deviceState.Torque / 100.0);
            TorqueFill.Width = Math.Max(0, Math.Min(torqueWidth, (TorqueFill.Parent as Border).ActualWidth));
            TorqueValueText.Text = $"{_deviceState.Torque:F1}%";

            // Change torque color based on level
            if (_deviceState.Torque > 80)
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
            else if (_deviceState.Torque > 60)
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12"));
            else
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60"));

            // Update status badge
            StatusText.Text = _deviceState.CurrentStatus;
            switch (_deviceState.CurrentStatus)
            {
                case "OPENING":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db"));
                    break;
                case "CLOSING":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e67e22"));
                    break;
                case "OPEN":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60"));
                    break;
                case "CLOSED":
                case "STOPPED":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));
                    break;
            }
        }

        private void OpenCommandButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_modbusService.IsConnected)
            {
                MessageBox.Show("Not connected to device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Send Open command via Modbus (Function Code 05 - Force Single Coil)
                // Coil address 0 = Open command
                bool success = _modbusService.WriteSingleCoil(0, true);

                if (!success)
                {
                    MessageBox.Show("Failed to send Open command", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending Open command: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseCommandButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_modbusService.IsConnected)
            {
                MessageBox.Show("Not connected to device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Send Close command via Modbus (Function Code 05 - Force Single Coil)
                // Coil address 1 = Close command
                bool success = _modbusService.WriteSingleCoil(1, true);

                if (!success)
                {
                    MessageBox.Show("Failed to send Close command", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending Close command: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopCommandButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_modbusService.IsConnected)
            {
                MessageBox.Show("Not connected to device", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Send Stop command via Modbus (Function Code 05 - Force Single Coil)
                // Coil address 2 = Stop command
                bool success = _modbusService.WriteSingleCoil(2, true);

                if (!success)
                {
                    MessageBox.Show("Failed to send Stop command", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending Stop command: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
