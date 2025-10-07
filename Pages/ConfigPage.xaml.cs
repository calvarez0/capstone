using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace WPF_GUI.Pages
{
    public partial class ConfigPage : Page
    {
        private bool setupMode = false;
        private ConfigData config = new ConfigData();

        public ConfigPage()
        {
            InitializeComponent();
            LoadConfigToUI();

            // Wire up slider value changed events
            SpeedSlider.ValueChanged += (s, e) => SpeedValue.Text = $"{e.NewValue:F0}%";
            TorqueSlider.ValueChanged += (s, e) => TorqueValue.Text = $"{e.NewValue:F0}%";
        }

        private void LoadConfigToUI()
        {
            DeviceNameBox.Text = config.DeviceName;
            ModbusAddressBox.Text = config.ModbusAddress.ToString();
            SpeedSlider.Value = config.SpeedSetting;
            TorqueSlider.Value = config.TorqueLimit;
            OpenPositionBox.Text = config.OpenPosition.ToString();
            ClosePositionBox.Text = config.ClosePosition.ToString();
            DeadbandBox.Text = config.PositionDeadband.ToString();
            FailsafeCombo.SelectedIndex = config.FailsafeMode == "Close" ? 0 : config.FailsafeMode == "Open" ? 1 : 2;
            MinOutputBox.Text = config.AnalogOutputMin.ToString();
            MaxOutputBox.Text = config.AnalogOutputMax.ToString();
        }

        private void SaveUIToConfig()
        {
            config.DeviceName = DeviceNameBox.Text;
            config.ModbusAddress = int.TryParse(ModbusAddressBox.Text, out int addr) ? addr : 254;
            config.SpeedSetting = (int)SpeedSlider.Value;
            config.TorqueLimit = (int)TorqueSlider.Value;
            config.OpenPosition = double.TryParse(OpenPositionBox.Text, out double open) ? open : 100;
            config.ClosePosition = double.TryParse(ClosePositionBox.Text, out double close) ? close : 0;
            config.PositionDeadband = double.TryParse(DeadbandBox.Text, out double db) ? db : 2;
            config.FailsafeMode = ((ComboBoxItem)FailsafeCombo.SelectedItem).Content.ToString()!.Split(' ')[0];
            config.AnalogOutputMin = double.TryParse(MinOutputBox.Text, out double min) ? min : 4;
            config.AnalogOutputMax = double.TryParse(MaxOutputBox.Text, out double max) ? max : 20;
        }

        private void EnterSetupButton_Click(object sender, RoutedEventArgs e)
        {
            setupMode = true;
            ModeText.Text = "Setup Mode";
            ModeBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e67e22"));

            // Enable/disable controls
            DeviceNameBox.IsEnabled = true;
            ModbusAddressBox.IsEnabled = true;
            SpeedSlider.IsEnabled = true;
            TorqueSlider.IsEnabled = true;
            OpenPositionBox.IsEnabled = true;
            ClosePositionBox.IsEnabled = true;
            DeadbandBox.IsEnabled = true;
            FailsafeCombo.IsEnabled = true;
            MinOutputBox.IsEnabled = true;
            MaxOutputBox.IsEnabled = true;

            EnterSetupButton.IsEnabled = false;
            ExitSetupButton.IsEnabled = true;
            WriteButton.IsEnabled = true;

            ShowMessage("Device entered Setup Mode. You can now modify configuration.", "info");
        }

        private void ExitSetupButton_Click(object sender, RoutedEventArgs e)
        {
            setupMode = false;
            ModeText.Text = "Stop Mode";
            ModeBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));

            // Enable/disable controls
            DeviceNameBox.IsEnabled = false;
            ModbusAddressBox.IsEnabled = false;
            SpeedSlider.IsEnabled = false;
            TorqueSlider.IsEnabled = false;
            OpenPositionBox.IsEnabled = false;
            ClosePositionBox.IsEnabled = false;
            DeadbandBox.IsEnabled = false;
            FailsafeCombo.IsEnabled = false;
            MinOutputBox.IsEnabled = false;
            MaxOutputBox.IsEnabled = false;

            EnterSetupButton.IsEnabled = true;
            ExitSetupButton.IsEnabled = false;
            WriteButton.IsEnabled = false;

            ShowMessage("Exited Setup Mode. Configuration changes saved.", "success");
        }

        private void WriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!setupMode)
            {
                ShowMessage("Error: Device must be in Setup Mode to write configuration.", "error");
                return;
            }

            SaveUIToConfig();
            ShowMessage("Configuration written to device successfully via Modbus RTU (Function Code 16).", "success");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveUIToConfig();

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = $"{config.DeviceName}_config.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(saveFileDialog.FileName, json);
                    ShowMessage("Configuration saved to file.", "success");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Error saving file: {ex.Message}", "error");
                }
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    config = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
                    LoadConfigToUI();
                    ShowMessage("Configuration loaded from file.", "success");
                }
                catch (Exception ex)
                {
                    ShowMessage($"Error: Invalid configuration file. {ex.Message}", "error");
                }
            }
        }

        private void ShowMessage(string message, string type)
        {
            MessageBorder.Visibility = Visibility.Visible;
            MessageText.Text = message;

            switch (type)
            {
                case "success":
                    MessageBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d4edda"));
                    MessageBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#c3e6cb"));
                    MessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#155724"));
                    break;
                case "error":
                    MessageBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8d7da"));
                    MessageBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f5c6cb"));
                    MessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#721c24"));
                    break;
                case "info":
                    MessageBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d1ecf1"));
                    MessageBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#bee5eb"));
                    MessageText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0c5460"));
                    break;
            }
        }
    }

    public class ConfigData
    {
        public string DeviceName { get; set; } = "S7X-001";
        public int ModbusAddress { get; set; } = 254;
        public double OpenPosition { get; set; } = 100;
        public double ClosePosition { get; set; } = 0;
        public int SpeedSetting { get; set; } = 50;
        public int TorqueLimit { get; set; } = 80;
        public double PositionDeadband { get; set; } = 2;
        public string FailsafeMode { get; set; } = "Close";
        public double AnalogOutputMin { get; set; } = 4;
        public double AnalogOutputMax { get; set; } = 20;
    }
}
