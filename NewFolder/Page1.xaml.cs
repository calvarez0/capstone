using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using WPF_GUI.Services;

namespace WPF_GUI.NewFolder
{
    /// <summary>
    /// Interaction logic for Page1.xaml
    /// </summary>
    public partial class Page1 : Page
    {
        private ModbusService _modbusService;

        public Page1()
        {
            InitializeComponent();
            _modbusService = App.ModbusService;

            // Subscribe to events
            _modbusService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _modbusService.ErrorOccurred += OnErrorOccurred;

            // Wire up button events
            OpenPortButton.Click += OpenPortButton_Click;
            ClosePortButton.Click += ClosePortButton_Click;

            // Update button states
            UpdateButtonStates();
        }

        private void OpenPortButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (!int.TryParse(PortNumberTextBox.Text, out int portNumber) || portNumber < 1)
                {
                    ShowError("Please enter a valid port number (1 or greater)");
                    return;
                }

                if (!byte.TryParse(ModbusAddressTextBox.Text, out byte modbusAddress) || modbusAddress < 1 || modbusAddress > 254)
                {
                    ShowError("Modbus address must be between 1 and 254");
                    return;
                }

                // Parse baud rate
                string baudRateText = ((ComboBoxItem)BaudRateComboBox.SelectedItem).Content.ToString();
                baudRateText = baudRateText.Replace(" (Default)", "").Trim();
                int baudRate = int.Parse(baudRateText);

                // Parse parity
                string parityText = ((ComboBoxItem)ParityComboBox.SelectedItem).Content.ToString();
                parityText = parityText.Replace(" (Default)", "").Trim();
                Parity parity = parityText switch
                {
                    "None" => Parity.None,
                    "Even" => Parity.Even,
                    "Odd" => Parity.Odd,
                    _ => Parity.None
                };

                // Parse stop bits
                string stopBitsText = ((ComboBoxItem)StopBitsComboBox.SelectedItem).Content.ToString();
                stopBitsText = stopBitsText.Replace(" (Default)", "").Trim();
                StopBits stopBits = stopBitsText == "1" ? StopBits.One : StopBits.Two;

                // Open the port
                bool success = _modbusService.OpenPort(portNumber.ToString(), baudRate, parity, stopBits, modbusAddress);

                if (success)
                {
                    // Disable connection settings
                    PortNumberTextBox.IsEnabled = false;
                    BaudRateComboBox.IsEnabled = false;
                    ParityComboBox.IsEnabled = false;
                    StopBitsComboBox.IsEnabled = false;
                    ModbusAddressTextBox.IsEnabled = false;

                    UpdateButtonStates();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to open port: {ex.Message}");
            }
        }

        private void ClosePortButton_Click(object sender, RoutedEventArgs e)
        {
            _modbusService.ClosePort();

            // Re-enable connection settings
            PortNumberTextBox.IsEnabled = true;
            BaudRateComboBox.IsEnabled = true;
            ParityComboBox.IsEnabled = true;
            StopBitsComboBox.IsEnabled = true;
            ModbusAddressTextBox.IsEnabled = true;

            UpdateButtonStates();
        }

        private void OnConnectionStatusChanged(object sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusMessageText.Text = message;
                StatusMessageBorder.Visibility = Visibility.Visible;

                // Change colors based on connection state
                if (_modbusService.IsConnected)
                {
                    StatusMessageBorder.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#d4edda"));
                    StatusMessageBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27ae60"));
                }
                else
                {
                    StatusMessageBorder.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#d1ecf1"));
                    StatusMessageBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3498db"));
                }
            });
        }

        private void OnErrorOccurred(object sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ShowError(message);
            });
        }

        private void ShowError(string message)
        {
            StatusMessageText.Text = message;
            StatusMessageBorder.Visibility = Visibility.Visible;
            StatusMessageBorder.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f8d7da"));
            StatusMessageBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e74c3c"));
        }

        private void UpdateButtonStates()
        {
            OpenPortButton.IsEnabled = !_modbusService.IsConnected;
            ClosePortButton.IsEnabled = _modbusService.IsConnected;
        }
    }
}
