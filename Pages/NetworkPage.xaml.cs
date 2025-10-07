using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WPF_GUI.Pages
{
    public partial class NetworkPage : Page
    {
        private List<Device> devices = new List<Device>();
        private DispatcherTimer? pollingTimer;
        private bool isPolling = false;
        private Random random = new Random();

        private readonly string[] deviceColors = new[]
        {
            "#3498db", "#27ae60", "#e67e22", "#9b59b6",
            "#e74c3c", "#1abc9c", "#f39c12", "#34495e"
        };

        public NetworkPage()
        {
            InitializeComponent();
            InitializeDevices();
        }

        private void InitializeDevices()
        {
            int numberOfDevices = int.TryParse(NumberOfDevicesBox.Text, out int n) ? Math.Min(8, Math.Max(1, n)) : 4;
            devices.Clear();

            for (int i = 0; i < numberOfDevices; i++)
            {
                devices.Add(new Device
                {
                    Address = i + 1,
                    ProductId = $"S7X-{(i + 1):D3}",
                    Position = random.NextDouble() * 100,
                    Status = random.NextDouble() > 0.1 ? "active" : "inactive",
                    Color = deviceColors[i % deviceColors.Length]
                });
            }

            UpdateDeviceDisplay();
        }

        private void UpdateDeviceDisplay()
        {
            DevicesPanel.Items.Clear();

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                var panel = CreateDevicePanel(device, i < devices.Count - 1);
                DevicesPanel.Items.Add(panel);
            }

            // Update active devices count
            int activeCount = devices.Count(d => d.Status == "active");
            ActiveDevicesText.Text = $"{activeCount} / {devices.Count}";
        }

        private StackPanel CreateDevicePanel(Device device, bool showConnection)
        {
            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(5)
            };

            // Device card
            var deviceCard = new Border
            {
                Background = Brushes.White,
                BorderBrush = device.Status == "active" ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(device.Color)) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Margin = new Thickness(5),
                Cursor = Cursors.Hand,
                MinWidth = 150
            };

            deviceCard.MouseLeftButtonDown += (s, e) => HandleDeviceClick(device);

            var cardContent = new StackPanel();

            // Device icon
            var icon = new Ellipse
            {
                Width = 50,
                Height = 50,
                Fill = device.Status == "active" ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(device.Color)) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            cardContent.Children.Add(icon);

            // Product ID
            var productId = new TextBlock
            {
                Text = device.ProductId,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            cardContent.Children.Add(productId);

            // Address
            var address = new TextBlock
            {
                Text = $"Address: {device.Address}",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6c757d")),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            cardContent.Children.Add(address);

            // Position or error
            if (device.Status == "active")
            {
                var position = new TextBlock
                {
                    Text = $"Position: {device.Position:F1}%",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")),
                    TextAlignment = TextAlignment.Center
                };
                cardContent.Children.Add(position);
            }
            else
            {
                var error = new TextBlock
                {
                    Text = "No Response",
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")),
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center
                };
                cardContent.Children.Add(error);
            }

            deviceCard.Child = cardContent;
            mainPanel.Children.Add(deviceCard);

            // Connection line
            if (showConnection)
            {
                var connectionPanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 5, 0)
                };

                bool nextActive = devices.Count > devices.IndexOf(device) + 1 &&
                                 devices[devices.IndexOf(device) + 1].Status == "active";

                var line = new Rectangle
                {
                    Width = 40,
                    Height = 3,
                    Fill = nextActive ?
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")) :
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                connectionPanel.Children.Add(line);

                var lineLabel = new TextBlock
                {
                    Text = nextActive ? "✓" : "✗",
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = nextActive ? Brushes.Green : Brushes.Red
                };
                connectionPanel.Children.Add(lineLabel);

                mainPanel.Children.Add(connectionPanel);
            }

            return mainPanel;
        }

        private void HandleDeviceClick(Device device)
        {
            MessageBox.Show($"Navigate to Configuration page for device at address {device.Address}",
                          "Device Selected", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void StartPollingButton_Click(object sender, RoutedEventArgs e)
        {
            isPolling = true;
            PollingIndicator.Visibility = Visibility.Visible;
            StartPollingButton.IsEnabled = false;
            StopPollingButton.IsEnabled = true;
            NumberOfDevicesBox.IsEnabled = false;
            PollingDirectionCombo.IsEnabled = false;
            CustomSequenceBox.IsEnabled = false;

            pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            pollingTimer.Tick += PollingTimer_Tick;
            pollingTimer.Start();
        }

        private void StopPollingButton_Click(object sender, RoutedEventArgs e)
        {
            isPolling = false;
            PollingIndicator.Visibility = Visibility.Collapsed;
            StartPollingButton.IsEnabled = true;
            StopPollingButton.IsEnabled = false;
            NumberOfDevicesBox.IsEnabled = true;
            PollingDirectionCombo.IsEnabled = true;
            CustomSequenceBox.IsEnabled = true;

            pollingTimer?.Stop();
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            // Update device positions
            foreach (var device in devices.Where(d => d.Status == "active"))
            {
                device.Position = random.NextDouble() * 100;
            }

            UpdateDeviceDisplay();
        }
    }

    public class Device
    {
        public int Address { get; set; }
        public string ProductId { get; set; } = "";
        public double Position { get; set; }
        public string Status { get; set; } = "active";
        public string Color { get; set; } = "#3498db";
    }
}
