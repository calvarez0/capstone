using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WPF_GUI.Pages
{
    public partial class ControlPage : Page
    {
        private double position = 50;
        private double torque = 45;
        private string status = "Stopped";
        private DispatcherTimer? pollingTimer;

        public ControlPage()
        {
            InitializeComponent();
            InitializePolling();
        }

        private void InitializePolling()
        {
            pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            pollingTimer.Tick += PollingTimer_Tick;
            pollingTimer.Start();
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            if (status == "Opening")
            {
                position = Math.Min(position + 2, 100);
                torque = new Random().NextDouble() * 20 + 40;

                if (position >= 100)
                {
                    status = "Open";
                    UpdateStatusBits(false, true, false);
                }

                UpdateDisplay();
            }
            else if (status == "Closing")
            {
                position = Math.Max(position - 2, 0);
                torque = new Random().NextDouble() * 20 + 40;

                if (position <= 0)
                {
                    status = "Closed";
                    UpdateStatusBits(false, false, true);
                }

                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            // Update position bar and value
            PositionFill.Width = (PositionFill.Parent as Grid)!.ActualWidth * (position / 100);
            PositionValue.Text = $"{position:F1}%";

            // Update torque bar and value with color
            TorqueFill.Width = (TorqueFill.Parent as Grid)!.ActualWidth * (torque / 100);
            TorqueValue.Text = $"{torque:F1}%";

            if (torque > 80)
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
            else if (torque > 60)
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12"));
            else
                TorqueFill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60"));

            // Update status badge
            StatusText.Text = status;
            StatusBadge.Background = status switch
            {
                "Opening" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")),
                "Closing" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c")),
                "Open" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498db")),
                "Closed" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e67e22")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"))
            };

            // Update button states
            OpenButton.IsEnabled = status != "Opening" && position < 100;
            CloseButton.IsEnabled = status != "Closing" && position > 0;
            StopButton.IsEnabled = status == "Opening" || status == "Closing";
        }

        private void UpdateStatusBits(bool moving, bool openLimit, bool closeLimit)
        {
            MovingBit.Fill = moving ?
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")) :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));

            OpenLimitBit.Fill = openLimit ?
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")) :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));

            CloseLimitBit.Fill = closeLimit ?
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27ae60")) :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            status = "Opening";
            UpdateStatusBits(true, false, false);
            UpdateDisplay();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            status = "Closing";
            UpdateStatusBits(true, false, false);
            UpdateDisplay();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            status = "Stopped";
            UpdateStatusBits(false, false, false);
            UpdateDisplay();
        }
    }
}
