using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WPF_GUI.Pages
{
    public partial class ConnectionPage : Page
    {
        private bool isConnected = false;

        public ConnectionPage()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            isConnected = true;

            // Disable input controls
            PortNumberBox.IsEnabled = false;
            BaudRateCombo.IsEnabled = false;
            ParityCombo.IsEnabled = false;
            StopBitsCombo.IsEnabled = false;
            ModbusAddressBox.IsEnabled = false;

            // Toggle buttons
            ConnectButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;

            // Show success message
            StatusBorder.Visibility = Visibility.Visible;
            StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d4edda"));
            StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c3e6cb"));
            StatusIcon.Text = "✓";
            StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#155724"));
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#155724"));

            string baudRate = ((ComboBoxItem)BaudRateCombo.SelectedItem).Content.ToString()!.Split(' ')[0];
            string parity = ((ComboBoxItem)ParityCombo.SelectedItem).Content.ToString()!.Split(' ')[0];
            string stopBits = ((ComboBoxItem)StopBitsCombo.SelectedItem).Content.ToString()!.Split(' ')[0];

            StatusText.Text = $"Connected to COM{PortNumberBox.Text} ({baudRate} baud, {parity}, {stopBits} stop bit{(stopBits != "1" ? "s" : "")}) - Modbus Address: {ModbusAddressBox.Text}";
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            isConnected = false;

            // Enable input controls
            PortNumberBox.IsEnabled = true;
            BaudRateCombo.IsEnabled = true;
            ParityCombo.IsEnabled = true;
            StopBitsCombo.IsEnabled = true;
            ModbusAddressBox.IsEnabled = true;

            // Toggle buttons
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;

            // Show disconnected message
            StatusBorder.Visibility = Visibility.Visible;
            StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1ecf1"));
            StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#bee5eb"));
            StatusIcon.Text = "ℹ";
            StatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0c5460"));
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0c5460"));
            StatusText.Text = "Disconnected";
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
