using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using ModbusActuatorControl;

namespace WPF_GUI.NewFolder
{
    /// <summary>
    /// Control page - handles device commands and status monitoring
    /// </summary>
    public partial class Page2 : Page
    {
        private DispatcherTimer _pollingTimer;
        private ActuatorDevice? _currentDevice;
        private bool _setupModeEnabled = false;

        public Page2()
        {
            InitializeComponent();

            // Wire up button events
            OpenCommandButton.Click += OpenCommandButton_Click;
            CloseCommandButton.Click += CloseCommandButton_Click;
            StopCommandButton.Click += StopCommandButton_Click;
            MoveToPositionButton.Click += MoveToPositionButton_Click;
            EsdCommandButton.Click += EsdCommandButton_Click;
            PstCommandButton.Click += PstCommandButton_Click;
            ToggleSetupModeButton.Click += ToggleSetupModeButton_Click;
            RefreshStatusButton.Click += RefreshStatusButton_Click;

            // Setup polling timer for UI updates
            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromMilliseconds(200);
            _pollingTimer.Tick += PollingTimer_Tick;

            // Subscribe to page loaded/unloaded
            this.Loaded += Page2_Loaded;
            this.Unloaded += Page2_Unloaded;

            // Subscribe to app events
            App.DeviceStatusUpdated += OnDeviceStatusUpdated;
            App.ModeChanged += OnModeChanged;
        }

        private void Page2_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateDeviceComboBox();
            _pollingTimer.Start();
        }

        private void Page2_Unloaded(object sender, RoutedEventArgs e)
        {
            _pollingTimer.Stop();
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
                UpdateUI();
            }
        }

        private void OnModeChanged(object? sender, bool isSimulation)
        {
            Dispatcher.Invoke(() =>
            {
                PopulateDeviceComboBox();
            });
        }

        private void OnDeviceStatusUpdated(object? sender, ActuatorDevice device)
        {
            if (device == _currentDevice)
            {
                Dispatcher.Invoke(() => UpdateUI());
            }
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentDevice != null)
            {
                try
                {
                    _currentDevice.UpdateStatus();
                    UpdateUI();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
                }
            }
        }

        private void RefreshStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice != null)
            {
                try
                {
                    _currentDevice.UpdateStatus();
                    UpdateUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to refresh: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void UpdateUI()
        {
            if (_currentDevice == null) return;

            var status = _currentDevice.CurrentStatus;

            // Update position bar
            double positionPercent = status.Position / 40.95; // Convert 0-4095 to 0-100
            var positionParent = PositionFill.Parent as Border;
            if (positionParent != null)
            {
                double positionWidth = positionParent.ActualWidth * (positionPercent / 100.0);
                PositionFill.Width = Math.Max(0, Math.Min(positionWidth, positionParent.ActualWidth));
            }
            PositionValueText.Text = $"{positionPercent:F1}%";

            // Update torque bar (using ValveTorque from Status)
            double torquePercent = status.Status.ValveTorque * 0.024; // ValveTorque 0-4095 = 0.024% each
            var torqueParent = TorqueFill.Parent as Border;
            if (torqueParent != null)
            {
                double torqueWidth = torqueParent.ActualWidth * (torquePercent / 100.0);
                TorqueFill.Width = Math.Max(0, Math.Min(torqueWidth, torqueParent.ActualWidth));
            }
            TorqueValueText.Text = $"{torquePercent:F1}%";

            // Change torque color based on level
            if (torquePercent > 80)
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")!);
            else if (torquePercent > 60)
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12")!);
            else
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);

            // Update status badge
            string statusText = GetStatusText(status);
            StatusText.Text = statusText;
            UpdateStatusBadgeColor(statusText);

            // Update status indicators
            UpdateIndicator(PowerOkIndicator, PowerOkDot, !status.Status.LossOfPowerAlarm);
            UpdateIndicator(CommIndicator, CommDot, !status.Status.LossOfSignalAlarm);
            UpdateIndicator(CalibratedIndicator, CalibratedDot, true); // No calibration flag in backend
            UpdateIndicator(MovingIndicator, MovingDot, status.IsMoving);
            UpdateIndicator(OpenLimitIndicator, OpenLimitDot, status.Status.LimitSwitchOpen);
            UpdateIndicator(CloseLimitIndicator, CloseLimitDot, status.Status.LimitSwitchClose);
            UpdateIndicator(SetupModeIndicator, SetupModeDot, status.Status.SetupMode);
            UpdateIndicator(TorqueLimitIndicator, TorqueLimitDot, status.Status.TorqueSwitchOpen || status.Status.TorqueSwitchClose);
            UpdateIndicator(LocalModeIndicator, LocalModeDot, status.Status.LocalMode);
            UpdateIndicator(ManualOverrideIndicator, ManualOverrideDot, status.Status.HandwheelPulledOut);

            // Update setup mode button
            _setupModeEnabled = status.Status.SetupMode;
            ToggleSetupModeButton.Content = _setupModeEnabled ? "Disable" : "Enable";
            SetupModeStatusText.Text = _setupModeEnabled ? "Active" : "Inactive";

            // Update detailed status
            DetailPositionText.Text = $"Position: {status.Position}";
            DetailSetpointText.Text = $"Setpoint: N/A";
            DetailAnalogInText.Text = $"Analog In 1: {status.Status.AnalogInput1}";
            DetailTorqueText.Text = $"Torque: {status.Status.ValveTorque}";
            DetailPeakTorqueText.Text = $"Close/Open Torque: {status.CloseTorque}%/{status.OpenTorque}%";
            DetailTorqueLimitText.Text = $"Torque Limit: N/A";
            DetailTempText.Text = $"Thermal Alarm: {status.Status.MotorThermalAlarm}";
            DetailMotorTempText.Text = $"Motor Thermal: {status.Status.MotorThermalAlarm}";
            DetailProductIdText.Text = $"Product ID: 0x{status.ProductIdentifier:X4}";
            DetailFirmwareText.Text = $"PST Result: {status.Status.PstResult}";
            DetailCycleCountText.Text = $"Timestamp: {status.Timestamp:HH:mm:ss}";

            // Update alarms
            UpdateAlarms(status);
        }

        private string GetStatusText(ActuatorStatus status)
        {
            if (status.IsMoving)
            {
                if (status.Status.ValveClosing)
                    return "CLOSING";
                else if (status.Status.ValveOpening)
                    return "OPENING";
                else
                    return "MOVING";
            }
            else if (status.Status.LimitSwitchOpen)
            {
                return "OPEN";
            }
            else if (status.Status.LimitSwitchClose)
            {
                return "CLOSED";
            }
            else if (status.Status.StopMode)
            {
                return "STOPPED";
            }
            else
            {
                return "IDLE";
            }
        }

        private void UpdateStatusBadgeColor(string statusText)
        {
            switch (statusText)
            {
                case "OPENING":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db")!);
                    break;
                case "CLOSING":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e67e22")!);
                    break;
                case "MOVING":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9b59b6")!);
                    break;
                case "OPEN":
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);
                    break;
                case "CLOSED":
                case "STOPPED":
                case "IDLE":
                default:
                    StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")!);
                    break;
            }
        }

        private void UpdateIndicator(Border indicator, Ellipse dot, bool active)
        {
            if (active)
            {
                indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d4edda")!);
                dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);
            }
            else
            {
                indicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f5f5f5")!);
                dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")!);
            }
        }

        private void UpdateAlarms(ActuatorStatus status)
        {
            bool hasAlarms = status.HasAnyAlarm;

            if (hasAlarms)
            {
                AlarmStatusIcon.Text = "WARNING";
                AlarmStatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")!);
                AlarmStatusText.Text = "Active alarms detected";
                AlarmStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")!);
                AlarmListBox.Visibility = Visibility.Visible;
                AlarmListBox.Items.Clear();
                if (status.Status.StallAlarm) AlarmListBox.Items.Add("Stall Alarm");
                if (status.Status.ValveDriftAlarm) AlarmListBox.Items.Add("Valve Drift Alarm");
                if (status.Status.EsdActiveAlarm) AlarmListBox.Items.Add("ESD Active");
                if (status.Status.MotorThermalAlarm) AlarmListBox.Items.Add("Motor Thermal Alarm");
                if (status.Status.LossOfPowerAlarm) AlarmListBox.Items.Add("Loss of Power");
                if (status.Status.LossOfSignalAlarm) AlarmListBox.Items.Add("Loss of Signal");
            }
            else
            {
                AlarmStatusIcon.Text = "No Alarms";
                AlarmStatusIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);
                AlarmStatusText.Text = "No active alarms";
                AlarmStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);
                AlarmListBox.Visibility = Visibility.Collapsed;
            }
        }

        // Command handlers - using DeviceStatus.WriteCommandsToDevice through master
        private void OpenCommandButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCommand(() =>
            {
                if (_currentDevice != null && App.Master != null)
                {
                    _currentDevice.CurrentStatus.Status.HostOpenCmd = true;
                    _currentDevice.CurrentStatus.Status.HostCloseCmd = false;
                    _currentDevice.CurrentStatus.Status.HostStopCmd = false;
                    _currentDevice.CurrentStatus.Status.WriteCommandsToDevice(App.Master, _currentDevice.SlaveId);
                }
            }, "Open");
        }

        private void CloseCommandButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCommand(() =>
            {
                if (_currentDevice != null && App.Master != null)
                {
                    _currentDevice.CurrentStatus.Status.HostOpenCmd = false;
                    _currentDevice.CurrentStatus.Status.HostCloseCmd = true;
                    _currentDevice.CurrentStatus.Status.HostStopCmd = false;
                    _currentDevice.CurrentStatus.Status.WriteCommandsToDevice(App.Master, _currentDevice.SlaveId);
                }
            }, "Close");
        }

        private void StopCommandButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCommand(() =>
            {
                if (_currentDevice != null)
                {
                    _currentDevice.Stop(true);
                }
            }, "Stop");
        }

        private void MoveToPositionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                MessageBox.Show("No device selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!ushort.TryParse(TargetPositionTextBox.Text, out ushort position) || position > 4095)
            {
                MessageBox.Show("Position must be between 0 and 4095", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExecuteCommand(() => _currentDevice.MoveToPosition(position), "Move to Position");
        }

        private void EsdCommandButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDevice == null)
            {
                MessageBox.Show("No device selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show("Are you sure you want to trigger Emergency Shutdown (ESD)?",
                "Confirm ESD", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ExecuteCommand(() =>
                {
                    if (_currentDevice != null && App.Master != null)
                    {
                        _currentDevice.CurrentStatus.Status.HostEsdCmd = true;
                        _currentDevice.CurrentStatus.Status.WriteCommandsToDevice(App.Master, _currentDevice.SlaveId);
                    }
                }, "ESD");
            }
        }

        private void PstCommandButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCommand(() =>
            {
                if (_currentDevice != null && App.Master != null)
                {
                    _currentDevice.CurrentStatus.Status.HostPstCmd = true;
                    _currentDevice.CurrentStatus.Status.WriteCommandsToDevice(App.Master, _currentDevice.SlaveId);
                }
            }, "PST (Partial Stroke Test)");
        }

        private void ToggleSetupModeButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteCommand(() =>
            {
                if (_currentDevice != null && App.Master != null)
                {
                    _currentDevice.CurrentStatus.Status.SoftSetupCmd = !_setupModeEnabled;
                    _currentDevice.CurrentStatus.Status.WriteCommandsToDevice(App.Master, _currentDevice.SlaveId);
                }
            }, "Toggle Setup Mode");
        }

        private void ExecuteCommand(Action commandAction, string commandName)
        {
            if (_currentDevice == null)
            {
                MessageBox.Show("No device selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                commandAction();
                System.Diagnostics.Debug.WriteLine($"{commandName} command sent to slave {_currentDevice.SlaveId}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send {commandName} command: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
