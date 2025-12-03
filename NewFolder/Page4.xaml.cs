using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ModbusActuatorControl;

namespace WPF_GUI.NewFolder
{
    /// <summary>
    /// Network visualization page - displays all devices and their status
    /// </summary>
    public partial class Page4 : Page
    {
        private ObservableCollection<DeviceViewModel> _deviceViewModels = new ObservableCollection<DeviceViewModel>();
        private DispatcherTimer _pollingTimer;
        private bool _isPolling = false;

        public Page4()
        {
            InitializeComponent();

            DeviceItemsControl.ItemsSource = _deviceViewModels;

            // Wire up button events
            StartPollingButton.Click += StartPollingButton_Click;
            StopPollingButton.Click += StopPollingButton_Click;
            RefreshAllButton.Click += RefreshAllButton_Click;

            // Setup polling timer
            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Tick += PollingTimer_Tick;

            // Subscribe to events
            this.Loaded += Page4_Loaded;
            this.Unloaded += Page4_Unloaded;
            App.ModeChanged += OnModeChanged;
            App.DeviceStatusUpdated += OnDeviceStatusUpdated;
        }

        private void Page4_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDeviceList();
            UpdateNetworkInfo();
        }

        private void Page4_Unloaded(object sender, RoutedEventArgs e)
        {
            StopPolling();
        }

        private void OnModeChanged(object? sender, bool isSimulation)
        {
            Dispatcher.Invoke(() =>
            {
                RefreshDeviceList();
                UpdateNetworkInfo();
            });
        }

        private void OnDeviceStatusUpdated(object? sender, ActuatorDevice device)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateDeviceViewModel(device);
            });
        }

        private void RefreshDeviceList()
        {
            _deviceViewModels.Clear();

            foreach (var device in App.Devices)
            {
                var vm = CreateDeviceViewModel(device);
                _deviceViewModels.Add(vm);
            }

            NoDevicesText.Visibility = _deviceViewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DeviceCountText.Text = _deviceViewModels.Count.ToString();
        }

        private DeviceViewModel CreateDeviceViewModel(ActuatorDevice device)
        {
            var status = device.CurrentStatus;
            double positionPercent = status.Position / 40.95;

            return new DeviceViewModel
            {
                SlaveId = device.SlaveId,
                DeviceName = $"{ProductCapabilities.GetProductName(status.ProductIdentifier)}-{device.SlaveId:D3}",
                ProductType = GetProductTypeString(status.ProductIdentifier),
                Position = positionPercent,
                PositionText = $"Position: {positionPercent:F1}%",
                AddressText = $"Address: {device.SlaveId}",
                StatusText = GetStatusText(status),
                IsMoving = status.IsMoving,
                BackgroundColor = GetBackgroundBrush(status),
                BorderColor = GetBorderBrush(status),
                StatusColor = GetStatusColor(status),
                StatusTextColor = GetStatusTextBrush(status),
                Device = device
            };
        }

        private void UpdateDeviceViewModel(ActuatorDevice device)
        {
            foreach (var vm in _deviceViewModels)
            {
                if (vm.Device == device)
                {
                    var status = device.CurrentStatus;
                    double positionPercent = status.Position / 40.95;

                    vm.Position = positionPercent;
                    vm.PositionText = $"Position: {positionPercent:F1}%";
                    vm.StatusText = GetStatusText(status);
                    vm.IsMoving = status.IsMoving;
                    vm.BackgroundColor = GetBackgroundBrush(status);
                    vm.BorderColor = GetBorderBrush(status);
                    vm.StatusColor = GetStatusColor(status);
                    vm.StatusTextColor = GetStatusTextBrush(status);
                    break;
                }
            }
        }

        private string GetProductTypeString(ushort productId)
        {
            return productId switch
            {
                0x8000 => "S/X",
                0x8001 => "EH",
                0x8002 => "Nova",
                _ => "Unknown"
            };
        }

        private string GetStatusText(ActuatorStatus status)
        {
            if (status.HasAnyAlarm) return "FAULT";
            if (status.IsMoving)
            {
                if (status.Status.ValveClosing) return "CLOSING";
                if (status.Status.ValveOpening) return "OPENING";
                return "MOVING";
            }
            if (status.Status.LimitSwitchOpen) return "OPEN";
            if (status.Status.LimitSwitchClose) return "CLOSED";
            return "STOPPED";
        }

        private Brush GetBackgroundBrush(ActuatorStatus status)
        {
            if (status.HasAnyAlarm) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8d7da")!);
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d4edda")!);
        }

        private Brush GetBorderBrush(ActuatorStatus status)
        {
            if (status.HasAnyAlarm) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")!);
            if (status.IsMoving) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db")!);
            if (status.Status.LimitSwitchOpen) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")!);
        }

        private string GetStatusColor(ActuatorStatus status)
        {
            if (status.HasAnyAlarm) return "Red";
            if (status.IsMoving) return "Yellow";
            if (status.Status.LimitSwitchOpen) return "Green";
            return "Transparent";
        }

        private Brush GetStatusTextBrush(ActuatorStatus status)
        {
            if (status.HasAnyAlarm) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")!);
            if (status.IsMoving) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db")!);
            if (status.Status.LimitSwitchOpen) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")!);
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")!);
        }

        private void UpdateNetworkInfo()
        {
            ModeText.Text = App.IsSimulationMode ? "Simulation" : "Hardware";
            DeviceCountText.Text = App.Devices.Count.ToString();

            if (App.CurrentConfig != null)
            {
                if (App.IsSimulationMode)
                {
                    ConnectionInfoText.Text = $"Simulated {App.CurrentConfig.BaudRate} baud";
                }
                else
                {
                    ConnectionInfoText.Text = $"{App.CurrentConfig.ComPort} @ {App.CurrentConfig.BaudRate} baud";
                }
            }
            else
            {
                ConnectionInfoText.Text = "";
            }
        }

        private void StartPollingButton_Click(object sender, RoutedEventArgs e)
        {
            StartPolling();
        }

        private void StopPollingButton_Click(object sender, RoutedEventArgs e)
        {
            StopPolling();
        }

        private void RefreshAllButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshAllDevices();
        }

        private void StartPolling()
        {
            if (_isPolling) return;

            if (!int.TryParse(PollingIntervalTextBox.Text, out int interval) || interval < 100)
            {
                interval = 500;
                PollingIntervalTextBox.Text = "500";
            }

            _pollingTimer.Interval = TimeSpan.FromMilliseconds(interval);
            _pollingTimer.Start();
            _isPolling = true;

            PollingIndicator.Visibility = Visibility.Visible;
            PollingStatusText.Text = $"Polling active - every {interval}ms";
            StartPollingButton.IsEnabled = false;
            StopPollingButton.IsEnabled = true;
        }

        private void StopPolling()
        {
            _pollingTimer.Stop();
            _isPolling = false;

            PollingIndicator.Visibility = Visibility.Collapsed;
            StartPollingButton.IsEnabled = true;
            StopPollingButton.IsEnabled = false;
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            RefreshAllDevices();
        }

        private void RefreshAllDevices()
        {
            foreach (var device in App.Devices)
            {
                try
                {
                    device.UpdateStatus();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error polling device {device.SlaveId}: {ex.Message}");
                }
            }

            // Update UI
            foreach (var device in App.Devices)
            {
                UpdateDeviceViewModel(device);
            }
        }

        private void DeviceCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is DeviceViewModel vm)
            {
                // Navigate to the Control page with this device selected
                // This would require passing the device to Page2
                MessageBox.Show($"Selected device: {vm.DeviceName}\nSlave ID: {vm.SlaveId}\nPosition: {vm.Position:F1}%\nStatus: {vm.StatusText}",
                    "Device Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    // ViewModel for device display
    public class DeviceViewModel : INotifyPropertyChanged
    {
        public ActuatorDevice? Device { get; set; }

        private byte _slaveId;
        public byte SlaveId
        {
            get => _slaveId;
            set { _slaveId = value; OnPropertyChanged(nameof(SlaveId)); }
        }

        private string _deviceName = "";
        public string DeviceName
        {
            get => _deviceName;
            set { _deviceName = value; OnPropertyChanged(nameof(DeviceName)); }
        }

        private string _productType = "";
        public string ProductType
        {
            get => _productType;
            set { _productType = value; OnPropertyChanged(nameof(ProductType)); }
        }

        private double _position;
        public double Position
        {
            get => _position;
            set { _position = value; OnPropertyChanged(nameof(Position)); }
        }

        private string _positionText = "";
        public string PositionText
        {
            get => _positionText;
            set { _positionText = value; OnPropertyChanged(nameof(PositionText)); }
        }

        private string _addressText = "";
        public string AddressText
        {
            get => _addressText;
            set { _addressText = value; OnPropertyChanged(nameof(AddressText)); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        private bool _isMoving;
        public bool IsMoving
        {
            get => _isMoving;
            set { _isMoving = value; OnPropertyChanged(nameof(IsMoving)); }
        }

        private Brush _backgroundColor = Brushes.White;
        public Brush BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); }
        }

        private Brush _borderColor = Brushes.Gray;
        public Brush BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; OnPropertyChanged(nameof(BorderColor)); }
        }

        private string _statusColor = "Transparent";
        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        private Brush _statusTextColor = Brushes.Black;
        public Brush StatusTextColor
        {
            get => _statusTextColor;
            set { _statusTextColor = value; OnPropertyChanged(nameof(StatusTextColor)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
