using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using ModbusActuatorControl;

namespace WPF_GUI.NewFolder
{
    /// <summary>
    /// Connection page - handles simulation and hardware connection settings
    /// </summary>
    public partial class Page1 : Page
    {
        private ObservableCollection<string> _deviceList = new ObservableCollection<string>();

        public Page1()
        {
            InitializeComponent();

            // Bind device list
            DeviceListBox.ItemsSource = _deviceList;

            // Subscribe to events
            App.ModeChanged += OnModeChanged;
            App.StatusChanged += OnStatusChanged;

            // Wire up button events
            OpenPortButton.Click += OpenPortButton_Click;
            ClosePortButton.Click += ClosePortButton_Click;
            ClearDevicesButton.Click += ClearDevicesButton_Click;
            CreateSimulationButton.Click += CreateSimulationButton_Click;
            ScanDevicesButton.Click += ScanDevicesButton_Click;

            // Update UI based on current state
            UpdateUIForMode();
            RefreshDeviceList();
        }

        private void OnModeChanged(object? sender, bool isSimulation)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateUIForMode();
                RefreshDeviceList();
            });
        }

        private void OnStatusChanged(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ShowStatus(message, App.IsConnected);
                RefreshDeviceList();
            });
        }

        private void UpdateUIForMode()
        {
            bool isSimulation = App.IsSimulationMode;
            bool isConnected = App.IsConnected;

            // Serial port settings - disabled in simulation mode, or when connected
            PortNumberTextBox.IsEnabled = !isSimulation && !isConnected;
            BaudRateComboBox.IsEnabled = true; // Always enabled for both modes
            ParityComboBox.IsEnabled = true;
            StopBitsComboBox.IsEnabled = true;

            // Simulation settings - only enabled in simulation mode
            ProductTypeComboBox.IsEnabled = isSimulation;
            StartingSlaveIdTextBox.IsEnabled = isSimulation;
            DeviceCountTextBox.IsEnabled = isSimulation;
            CreateSimulationButton.IsEnabled = isSimulation;

            // Hardware settings - only enabled in hardware mode when connected
            ScanStartIdTextBox.IsEnabled = !isSimulation && isConnected;
            ScanEndIdTextBox.IsEnabled = !isSimulation && isConnected;
            ScanDevicesButton.IsEnabled = !isSimulation && isConnected;

            // Connection buttons
            OpenPortButton.IsEnabled = !isSimulation && !isConnected;
            ClosePortButton.IsEnabled = isConnected;
            ClearDevicesButton.IsEnabled = App.Devices.Count > 0;

            // Show mode-specific status
            if (isSimulation)
            {
                ShowStatus($"Simulation Mode - {App.Devices.Count} device(s) active", true);
            }
            else if (!isConnected)
            {
                StatusMessageBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void CreateSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Parse settings
                if (!byte.TryParse(StartingSlaveIdTextBox.Text, out byte startId) || startId < 1 || startId > 254)
                {
                    ShowError("Starting Slave ID must be between 1 and 254");
                    return;
                }

                if (!int.TryParse(DeviceCountTextBox.Text, out int count) || count < 1 || count > 10)
                {
                    ShowError("Number of devices must be between 1 and 10");
                    return;
                }

                // Get product type
                var selectedItem = (ComboBoxItem)ProductTypeComboBox.SelectedItem;
                ushort productId = ushort.Parse(selectedItem.Tag.ToString()!);

                // Get baud rate
                int baudRate = GetSelectedBaudRate();
                int parity = ParityComboBox.SelectedIndex;
                int stopBits = StopBitsComboBox.SelectedIndex + 1;

                // Create simulated devices
                App.CreateSimulatedDevices(baudRate, parity, stopBits, startId, count, productId);

                RefreshDeviceList();
                UpdateUIForMode();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to create simulation: {ex.Message}");
            }
        }

        private void OpenPortButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.IsSimulationMode)
                {
                    ShowError("Cannot connect to hardware in simulation mode. Switch to Hardware mode first.");
                    return;
                }

                // Validate port number
                if (!int.TryParse(PortNumberTextBox.Text, out int portNumber) || portNumber < 1)
                {
                    ShowError("Please enter a valid port number (1 or greater)");
                    return;
                }

                string portName = $"COM{portNumber}";
                int baudRate = GetSelectedBaudRate();
                Parity parity = GetSelectedParity();
                StopBits stopBits = GetSelectedStopBits();

                bool success = App.ConnectToPort(portName, baudRate, parity, stopBits);

                if (success)
                {
                    UpdateUIForMode();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to open port: {ex.Message}");
            }
        }

        private void ClosePortButton_Click(object sender, RoutedEventArgs e)
        {
            App.Disconnect();
            RefreshDeviceList();
            UpdateUIForMode();
        }

        private void ClearDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            App.Cleanup();
            RefreshDeviceList();
            UpdateUIForMode();
            ShowStatus("All devices cleared", false);
        }

        private void ScanDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!byte.TryParse(ScanStartIdTextBox.Text, out byte startId))
                {
                    ShowError("Invalid start ID");
                    return;
                }

                if (!byte.TryParse(ScanEndIdTextBox.Text, out byte endId))
                {
                    ShowError("Invalid end ID");
                    return;
                }

                if (startId > endId)
                {
                    ShowError("Start ID must be less than or equal to End ID");
                    return;
                }

                App.ScanDevices(startId, endId);
                RefreshDeviceList();
                UpdateUIForMode();
            }
            catch (Exception ex)
            {
                ShowError($"Scan failed: {ex.Message}");
            }
        }

        private void RefreshDeviceList()
        {
            _deviceList.Clear();

            foreach (var device in App.Devices)
            {
                try
                {
                    device.UpdateStatus();
                    string productName = ProductCapabilities.GetProductName(device.CurrentStatus.ProductIdentifier);
                    _deviceList.Add($"Slave {device.SlaveId}: {productName} - Position: {device.CurrentStatus.Position}");
                }
                catch
                {
                    _deviceList.Add($"Slave {device.SlaveId}: (Status unavailable)");
                }
            }

            DeviceCountText.Text = App.Devices.Count > 0
                ? $"{App.Devices.Count} device(s) connected"
                : "No devices connected";
        }

        private int GetSelectedBaudRate()
        {
            string text = ((ComboBoxItem)BaudRateComboBox.SelectedItem).Content.ToString()!;
            text = text.Replace(" (Default)", "").Trim();
            return int.Parse(text);
        }

        private Parity GetSelectedParity()
        {
            string text = ((ComboBoxItem)ParityComboBox.SelectedItem).Content.ToString()!;
            text = text.Replace(" (Default)", "").Trim();
            return text switch
            {
                "None" => Parity.None,
                "Even" => Parity.Even,
                "Odd" => Parity.Odd,
                _ => Parity.None
            };
        }

        private StopBits GetSelectedStopBits()
        {
            string text = ((ComboBoxItem)StopBitsComboBox.SelectedItem).Content.ToString()!;
            text = text.Replace(" (Default)", "").Trim();
            return text == "1" ? StopBits.One : StopBits.Two;
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            StatusMessageText.Text = message;
            StatusMessageBorder.Visibility = Visibility.Visible;

            if (isSuccess)
            {
                StatusMessageBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d4edda"));
                StatusMessageBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60"));
                StatusIcon.Text = "\u2713";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60"));
            }
            else
            {
                StatusMessageBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1ecf1"));
                StatusMessageBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db"));
                StatusIcon.Text = "\u2139";
                StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db"));
            }
        }

        private void ShowError(string message)
        {
            StatusMessageText.Text = message;
            StatusMessageBorder.Visibility = Visibility.Visible;
            StatusMessageBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8d7da"));
            StatusMessageBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
            StatusIcon.Text = "\u2717";
            StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
        }
    }
}
