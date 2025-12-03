using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ModbusActuatorControl;

namespace WPF_GUI.NewFolder
{
    /// <summary>
    /// Configuration page - handles all device configuration settings
    /// </summary>
    public partial class Page3 : Page
    {
        private ActuatorDevice? _currentDevice;
        private DeviceConfig _currentConfig = new DeviceConfig();

        public Page3()
        {
            InitializeComponent();

            // Wire up button events
            ToggleSetupModeButton.Click += ToggleSetupModeButton_Click;
            ReadFromDeviceButton.Click += ReadFromDeviceButton_Click;
            WriteToDeviceButton.Click += WriteToDeviceButton_Click;
            WriteCalibrationButton.Click += WriteCalibrationButton_Click;
            SaveToFileButton.Click += SaveToFileButton_Click;
            LoadFromFileButton.Click += LoadFromFileButton_Click;

            // Set default selections
            ControlModeComboBox.SelectedIndex = 0;
            NetworkAdapterComboBox.SelectedIndex = 0;
            FailsafeActionComboBox.SelectedIndex = 0;
            EsdActionComboBox.SelectedIndex = 0;
            LossCommActionComboBox.SelectedIndex = 0;
            NetworkBaudRateComboBox.SelectedIndex = 3; // 9600 default
            CommParityComboBox.SelectedIndex = 0;

            // Subscribe to events
            this.Loaded += Page3_Loaded;
            App.ModeChanged += OnModeChanged;
        }

        private void Page3_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateDeviceComboBox();
        }

        private void OnModeChanged(object? sender, bool isSimulation)
        {
            Dispatcher.Invoke(() =>
            {
                PopulateDeviceComboBox();
            });
        }

        private void PopulateDeviceComboBox()
        {
            DeviceComboBox.Items.Clear();

            foreach (var device in App.Devices)
            {
                string productName = ProductCapabilities.GetProductName(device.CurrentStatus.ProductIdentifier);
                DeviceComboBox.Items.Add($"Slave {device.SlaveId} - {productName}");
            }

            if (DeviceComboBox.Items.Count > 0)
            {
                DeviceComboBox.SelectedIndex = 0;
            }
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeviceComboBox.SelectedIndex >= 0 && DeviceComboBox.SelectedIndex < App.Devices.Count)
            {
                _currentDevice = App.Devices[DeviceComboBox.SelectedIndex];
                UpdateProductTypeText();
                UpdateSetupModeDisplay();

                // Try to read configuration from device
                try
                {
                    var config = _currentDevice.ReadConfiguration();
                    _currentConfig = config.Config;
                    LoadConfigToUI();
                }
                catch
                {
                    // Use default config if read fails
                    _currentConfig = new DeviceConfig();
                    LoadConfigToUI();
                }
            }
        }

        private void UpdateProductTypeText()
        {
            if (_currentDevice != null)
            {
                string productName = ProductCapabilities.GetProductName(_currentDevice.CurrentStatus.ProductIdentifier);
                ProductTypeText.Text = $"Product: {productName} (0x{_currentDevice.CurrentStatus.ProductIdentifier:X4})";
            }
        }

        private void UpdateSetupModeDisplay()
        {
            if (_currentDevice != null)
            {
                bool setupMode = _currentDevice.CurrentStatus.Status.SetupMode;
                SetupModeText.Text = setupMode ? "ACTIVE" : "INACTIVE";
                SetupModeBadge.Background = new SolidColorBrush(
                    setupMode ? (Color)ColorConverter.ConvertFromString("#27ae60")! : (Color)ColorConverter.ConvertFromString("#95a5a6")!);
                ToggleSetupModeButton.Content = setupMode ? "Exit Setup Mode" : "Enter Setup Mode";
            }
        }

        private void ToggleSetupModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                ShowStatus("No device selected", false);
                return;
            }

            if (App.Master == null)
            {
                ShowStatus("Not connected", false);
                return;
            }

            try
            {
                bool currentSetupMode = _currentDevice.CurrentStatus.Status.SetupMode;
                _currentDevice.CurrentStatus.Status.SoftSetupCmd = !currentSetupMode;
                _currentDevice.CurrentStatus.Status.WriteCommandsToDevice(App.Master, _currentDevice.SlaveId);
                _currentDevice.UpdateStatus();
                UpdateSetupModeDisplay();
                ShowStatus("Setup mode toggled", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to toggle setup mode: {ex.Message}", false);
            }
        }

        private void LoadConfigToUI()
        {
            // Register 11 flags
            EhoTypeCheckBox.IsChecked = _currentConfig.EhoType == EhoType.SpringReturn;
            LocalInputMomentaryCheckBox.IsChecked = _currentConfig.LocalInputFunction == InputFunction.Momentary;
            RemoteInputMomentaryCheckBox.IsChecked = _currentConfig.RemoteInputFunction == InputFunction.Momentary;
            RemoteEsdEnabledCheckBox.IsChecked = _currentConfig.RemoteEsdEnabled == EnabledState.Enabled;
            LossCommEnabledCheckBox.IsChecked = _currentConfig.LossCommEnabled == EnabledState.Enabled;
            Ai1PolarityCheckBox.IsChecked = _currentConfig.Ai1Polarity == Polarity.Reversed;
            Ai2PolarityCheckBox.IsChecked = _currentConfig.Ai2Polarity == Polarity.Reversed;
            Ao1PolarityCheckBox.IsChecked = _currentConfig.Ao1Polarity == Polarity.Reversed;
            Ao2PolarityCheckBox.IsChecked = _currentConfig.Ao2Polarity == Polarity.Reversed;
            Di1TriggerCheckBox.IsChecked = _currentConfig.Di1OpenTrigger == TriggerType.NormallyClose;
            Di2TriggerCheckBox.IsChecked = _currentConfig.Di2CloseTrigger == TriggerType.NormallyClose;
            Di3TriggerCheckBox.IsChecked = _currentConfig.Di3StopTrigger == TriggerType.NormallyClose;
            Di4TriggerCheckBox.IsChecked = _currentConfig.Di4EsdTrigger == TriggerType.NormallyClose;
            Di5TriggerCheckBox.IsChecked = _currentConfig.Di5PstTrigger == TriggerType.NormallyClose;
            CloseDirectionCheckBox.IsChecked = _currentConfig.CloseDirection == CloseDirection.CounterClockwise;
            SeatModeCheckBox.IsChecked = _currentConfig.Seat == SeatMode.Torque;

            // Register 12 flags
            TorqueBackseatCheckBox.IsChecked = _currentConfig.TorqueBackseat == EnabledState.Enabled;
            TorqueRetryCheckBox.IsChecked = _currentConfig.TorqueRetry == EnabledState.Enabled;
            RemoteDisplayCheckBox.IsChecked = _currentConfig.RemoteDisplay == EnabledState.Enabled;
            LedsCheckBox.IsChecked = _currentConfig.Leds == EnabledState.Enabled;
            OpenInhibitCheckBox.IsChecked = _currentConfig.OpenInhibit == EnabledState.Enabled;
            CloseInhibitCheckBox.IsChecked = _currentConfig.CloseInhibit == EnabledState.Enabled;
            LocalEsdCheckBox.IsChecked = _currentConfig.LocalEsd == EnabledState.Enabled;
            EsdOrThermalCheckBox.IsChecked = _currentConfig.EsdOrThermal == EnabledState.Enabled;
            EsdOrLocalCheckBox.IsChecked = _currentConfig.EsdOrLocal == EnabledState.Enabled;
            EsdOrStopCheckBox.IsChecked = _currentConfig.EsdOrStop == EnabledState.Enabled;
            EsdOrInhibitCheckBox.IsChecked = _currentConfig.EsdOrInhibit == EnabledState.Enabled;
            EsdOrTorqueCheckBox.IsChecked = _currentConfig.EsdOrTorque == EnabledState.Enabled;
            CloseSpeedControlCheckBox.IsChecked = _currentConfig.CloseSpeedControl == EnabledState.Enabled;
            OpenSpeedControlCheckBox.IsChecked = _currentConfig.OpenSpeedControl == EnabledState.Enabled;

            // Control config
            ControlModeComboBox.SelectedIndex = (int)_currentConfig.ControlMode;
            ModulationDelayTextBox.Text = _currentConfig.ModulationDelay.ToString();
            DeadbandTextBox.Text = _currentConfig.Deadband.ToString();
            NetworkAdapterComboBox.SelectedIndex = (int)_currentConfig.NetworkAdapter;

            // Additional functions
            FailsafeActionComboBox.SelectedIndex = (int)_currentConfig.FailsafeFunction;
            FailsafePositionTextBox.Text = _currentConfig.FailsafeGoToPosition.ToString();
            EsdActionComboBox.SelectedIndex = (int)_currentConfig.EsdFunction;
            EsdDelayTextBox.Text = _currentConfig.EsdDelay.ToString();
            LossCommActionComboBox.SelectedIndex = (int)_currentConfig.LossCommFunction;
            LossCommDelayTextBox.Text = _currentConfig.LossCommDelay.ToString();

            // Network
            NetworkBaudRateComboBox.SelectedIndex = (int)_currentConfig.NetworkBaudRate;
            ResponseDelayTextBox.Text = _currentConfig.NetworkResponseDelay.ToString();
            CommParityComboBox.SelectedIndex = (int)_currentConfig.NetworkCommParity;
            LsaTextBox.Text = _currentConfig.LSA.ToString();
            LsbTextBox.Text = _currentConfig.LSB.ToString();

            // Speed control
            OpenSpeedStartTextBox.Text = _currentConfig.OpenSpeedControlStart.ToString();
            OpenSpeedRatioTextBox.Text = _currentConfig.OpenSpeedControlRatio.ToString();
            CloseSpeedStartTextBox.Text = _currentConfig.CloseSpeedControlStart.ToString();
            CloseSpeedRatioTextBox.Text = _currentConfig.CloseSpeedControlRatio.ToString();

            // Calibration
            AI1ZeroTextBox.Text = _currentConfig.AnalogInput1ZeroCalibration.ToString();
            AI1SpanTextBox.Text = _currentConfig.AnalogInput1SpanCalibration.ToString();
            AI2ZeroTextBox.Text = _currentConfig.AnalogInput2ZeroCalibration.ToString();
            AI2SpanTextBox.Text = _currentConfig.AnalogInput2SpanCalibration.ToString();
            AO1ZeroTextBox.Text = _currentConfig.AnalogOutput1ZeroCalibration.ToString();
            AO1SpanTextBox.Text = _currentConfig.AnalogOutput1SpanCalibration.ToString();
            AO2ZeroTextBox.Text = _currentConfig.AnalogOutput2ZeroCalibration.ToString();
            AO2SpanTextBox.Text = _currentConfig.AnalogOutput2SpanCalibration.ToString();
        }

        private void SaveUIToConfig()
        {
            // Register 11 flags
            _currentConfig.EhoType = EhoTypeCheckBox.IsChecked == true ? EhoType.SpringReturn : EhoType.DoubleAction;
            _currentConfig.LocalInputFunction = LocalInputMomentaryCheckBox.IsChecked == true ? InputFunction.Momentary : InputFunction.Maintained;
            _currentConfig.RemoteInputFunction = RemoteInputMomentaryCheckBox.IsChecked == true ? InputFunction.Momentary : InputFunction.Maintained;
            _currentConfig.RemoteEsdEnabled = RemoteEsdEnabledCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.LossCommEnabled = LossCommEnabledCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.Ai1Polarity = Ai1PolarityCheckBox.IsChecked == true ? Polarity.Reversed : Polarity.Normal;
            _currentConfig.Ai2Polarity = Ai2PolarityCheckBox.IsChecked == true ? Polarity.Reversed : Polarity.Normal;
            _currentConfig.Ao1Polarity = Ao1PolarityCheckBox.IsChecked == true ? Polarity.Reversed : Polarity.Normal;
            _currentConfig.Ao2Polarity = Ao2PolarityCheckBox.IsChecked == true ? Polarity.Reversed : Polarity.Normal;
            _currentConfig.Di1OpenTrigger = Di1TriggerCheckBox.IsChecked == true ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            _currentConfig.Di2CloseTrigger = Di2TriggerCheckBox.IsChecked == true ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            _currentConfig.Di3StopTrigger = Di3TriggerCheckBox.IsChecked == true ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            _currentConfig.Di4EsdTrigger = Di4TriggerCheckBox.IsChecked == true ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            _currentConfig.Di5PstTrigger = Di5TriggerCheckBox.IsChecked == true ? TriggerType.NormallyClose : TriggerType.NormallyOpen;
            _currentConfig.CloseDirection = CloseDirectionCheckBox.IsChecked == true ? CloseDirection.CounterClockwise : CloseDirection.Clockwise;
            _currentConfig.Seat = SeatModeCheckBox.IsChecked == true ? SeatMode.Torque : SeatMode.Position;

            // Register 12 flags
            _currentConfig.TorqueBackseat = TorqueBackseatCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.TorqueRetry = TorqueRetryCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.RemoteDisplay = RemoteDisplayCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.Leds = LedsCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.OpenInhibit = OpenInhibitCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.CloseInhibit = CloseInhibitCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.LocalEsd = LocalEsdCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.EsdOrThermal = EsdOrThermalCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.EsdOrLocal = EsdOrLocalCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.EsdOrStop = EsdOrStopCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.EsdOrInhibit = EsdOrInhibitCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.EsdOrTorque = EsdOrTorqueCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.CloseSpeedControl = CloseSpeedControlCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;
            _currentConfig.OpenSpeedControl = OpenSpeedControlCheckBox.IsChecked == true ? EnabledState.Enabled : EnabledState.Disabled;

            // Control config
            _currentConfig.ControlMode = (ControlMode)ControlModeComboBox.SelectedIndex;
            _currentConfig.ModulationDelay = byte.TryParse(ModulationDelayTextBox.Text, out byte modDelay) ? modDelay : (byte)1;
            _currentConfig.Deadband = byte.TryParse(DeadbandTextBox.Text, out byte deadband) ? deadband : (byte)20;
            _currentConfig.NetworkAdapter = (NetworkAdapter)NetworkAdapterComboBox.SelectedIndex;

            // Additional functions
            _currentConfig.FailsafeFunction = (FunctionAction)FailsafeActionComboBox.SelectedIndex;
            _currentConfig.FailsafeGoToPosition = byte.TryParse(FailsafePositionTextBox.Text, out byte failPos) ? failPos : (byte)50;
            _currentConfig.EsdFunction = (FunctionAction)EsdActionComboBox.SelectedIndex;
            _currentConfig.EsdDelay = byte.TryParse(EsdDelayTextBox.Text, out byte esdDelay) ? esdDelay : (byte)0;
            _currentConfig.LossCommFunction = (FunctionAction)LossCommActionComboBox.SelectedIndex;
            _currentConfig.LossCommDelay = byte.TryParse(LossCommDelayTextBox.Text, out byte lossDelay) ? lossDelay : (byte)0;

            // Network
            _currentConfig.NetworkBaudRate = (NetworkBaudRate)NetworkBaudRateComboBox.SelectedIndex;
            _currentConfig.NetworkResponseDelay = byte.TryParse(ResponseDelayTextBox.Text, out byte respDelay) ? respDelay : (byte)8;
            _currentConfig.NetworkCommParity = (NetworkCommParity)CommParityComboBox.SelectedIndex;
            _currentConfig.LSA = byte.TryParse(LsaTextBox.Text, out byte lsa) ? lsa : (byte)25;
            _currentConfig.LSB = byte.TryParse(LsbTextBox.Text, out byte lsb) ? lsb : (byte)75;

            // Speed control
            _currentConfig.OpenSpeedControlStart = byte.TryParse(OpenSpeedStartTextBox.Text, out byte openStart) ? openStart : (byte)70;
            _currentConfig.OpenSpeedControlRatio = byte.TryParse(OpenSpeedRatioTextBox.Text, out byte openRatio) ? openRatio : (byte)50;
            _currentConfig.CloseSpeedControlStart = byte.TryParse(CloseSpeedStartTextBox.Text, out byte closeStart) ? closeStart : (byte)30;
            _currentConfig.CloseSpeedControlRatio = byte.TryParse(CloseSpeedRatioTextBox.Text, out byte closeRatio) ? closeRatio : (byte)50;

            // Calibration
            _currentConfig.AnalogInput1ZeroCalibration = ushort.TryParse(AI1ZeroTextBox.Text, out ushort ai1z) ? ai1z : (ushort)0;
            _currentConfig.AnalogInput1SpanCalibration = ushort.TryParse(AI1SpanTextBox.Text, out ushort ai1s) ? ai1s : (ushort)4095;
            _currentConfig.AnalogInput2ZeroCalibration = ushort.TryParse(AI2ZeroTextBox.Text, out ushort ai2z) ? ai2z : (ushort)0;
            _currentConfig.AnalogInput2SpanCalibration = ushort.TryParse(AI2SpanTextBox.Text, out ushort ai2s) ? ai2s : (ushort)4095;
            _currentConfig.AnalogOutput1ZeroCalibration = ushort.TryParse(AO1ZeroTextBox.Text, out ushort ao1z) ? ao1z : (ushort)0;
            _currentConfig.AnalogOutput1SpanCalibration = ushort.TryParse(AO1SpanTextBox.Text, out ushort ao1s) ? ao1s : (ushort)4095;
            _currentConfig.AnalogOutput2ZeroCalibration = ushort.TryParse(AO2ZeroTextBox.Text, out ushort ao2z) ? ao2z : (ushort)0;
            _currentConfig.AnalogOutput2SpanCalibration = ushort.TryParse(AO2SpanTextBox.Text, out ushort ao2s) ? ao2s : (ushort)4095;
        }

        private void ReadFromDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                ShowStatus("No device selected", false);
                return;
            }

            try
            {
                var config = _currentDevice.ReadConfiguration();
                _currentConfig = config.Config;
                LoadConfigToUI();
                ShowStatus("Configuration read from device", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to read configuration: {ex.Message}", false);
            }
        }

        private void WriteToDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                ShowStatus("No device selected", false);
                return;
            }

            if (App.Master == null)
            {
                ShowStatus("Not connected", false);
                return;
            }

            try
            {
                SaveUIToConfig();
                _currentConfig.WriteToDevice(App.Master, _currentDevice.SlaveId);
                ShowStatus("Configuration written to device", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to write configuration: {ex.Message}", false);
            }
        }

        private void WriteCalibrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                ShowStatus("No device selected", false);
                return;
            }

            if (App.Master == null)
            {
                ShowStatus("Not connected", false);
                return;
            }

            try
            {
                SaveUIToConfig();
                _currentConfig.WriteCalibrationToDevice(App.Master, _currentDevice.SlaveId);
                ShowStatus("Calibration values written to device", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to write calibration: {ex.Message}", false);
            }
        }

        private void SaveToFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                ShowStatus("No device selected", false);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = "json",
                FileName = $"config_slave{_currentDevice.SlaveId}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    SaveUIToConfig();

                    // Create system config with this device
                    var sysConfig = App.CurrentConfig ?? new SystemConfig();
                    sysConfig.Actuators.Clear();
                    sysConfig.Actuators.Add(new ActuatorConfig
                    {
                        SlaveId = _currentDevice.SlaveId,
                        DeviceName = $"Device {_currentDevice.SlaveId}",
                        ProductIdentifier = _currentDevice.CurrentStatus.ProductIdentifier,
                        Config = _currentConfig
                    });

                    sysConfig.SaveToFile(dialog.FileName);
                    ShowStatus($"Configuration saved to {dialog.FileName}", true);
                }
                catch (Exception ex)
                {
                    ShowStatus($"Failed to save: {ex.Message}", false);
                }
            }
        }

        private void LoadFromFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sysConfig = SystemConfig.LoadFromFile(dialog.FileName);

                    if (sysConfig.Actuators.Count > 0)
                    {
                        _currentConfig = sysConfig.Actuators[0].Config;
                        LoadConfigToUI();
                        ShowStatus($"Configuration loaded from {dialog.FileName}", true);
                    }
                    else
                    {
                        ShowStatus("No actuator configuration found in file", false);
                    }
                }
                catch (Exception ex)
                {
                    ShowStatus($"Failed to load: {ex.Message}", false);
                }
            }
        }

        private void ShowStatus(string message, bool isSuccess)
        {
            StatusText.Text = message;
            StatusBorder.Visibility = Visibility.Visible;

            if (isSuccess)
            {
                StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d4edda")!);
                StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#155724")!);
            }
            else
            {
                StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8d7da")!);
                StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")!);
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#721c24")!);
            }
        }
    }
}
