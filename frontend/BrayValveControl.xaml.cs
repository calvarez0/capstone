using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WPF_GUI
{
    public partial class BrayValveControl : UserControl
    {
        // Dependency Properties
        public static readonly DependencyProperty PositionProperty =
            DependencyProperty.Register("Position", typeof(double), typeof(BrayValveControl),
                new PropertyMetadata(0.0, OnPositionChanged));

        public static readonly DependencyProperty DeviceAddressProperty =
            DependencyProperty.Register("DeviceAddress", typeof(int), typeof(BrayValveControl),
                new PropertyMetadata(1, OnDeviceAddressChanged));

        public static readonly DependencyProperty DeviceTypeProperty =
            DependencyProperty.Register("DeviceType", typeof(string), typeof(BrayValveControl),
                new PropertyMetadata("S/X", OnDeviceTypeChanged));

        public static readonly DependencyProperty StatusColorProperty =
            DependencyProperty.Register("StatusColor", typeof(Color), typeof(BrayValveControl),
                new PropertyMetadata(Colors.Green, OnStatusColorChanged));

        public static readonly DependencyProperty IsBlinkingProperty =
            DependencyProperty.Register("IsBlinking", typeof(bool), typeof(BrayValveControl),
                new PropertyMetadata(false, OnIsBlinkingChanged));

        // Properties
        public double Position
        {
            get { return (double)GetValue(PositionProperty); }
            set { SetValue(PositionProperty, value); }
        }

        public int DeviceAddress
        {
            get { return (int)GetValue(DeviceAddressProperty); }
            set { SetValue(DeviceAddressProperty, value); }
        }

        public string DeviceType
        {
            get { return (string)GetValue(DeviceTypeProperty); }
            set { SetValue(DeviceTypeProperty, value); }
        }

        public Color StatusColor
        {
            get { return (Color)GetValue(StatusColorProperty); }
            set { SetValue(StatusColorProperty, value); }
        }

        public bool IsBlinking
        {
            get { return (bool)GetValue(IsBlinkingProperty); }
            set { SetValue(IsBlinkingProperty, value); }
        }

        public BrayValveControl()
        {
            InitializeComponent();
            UpdateVisuals();
        }

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (BrayValveControl)d;
            control.UpdatePosition();
        }

        private static void OnDeviceAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (BrayValveControl)d;
            control.UpdateDeviceAddress();
        }

        private static void OnDeviceTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (BrayValveControl)d;
            control.UpdateDeviceType();
        }

        private static void OnStatusColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (BrayValveControl)d;
            control.UpdateStatusColor();
        }

        private static void OnIsBlinkingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (BrayValveControl)d;
            control.UpdateBlinking();
        }

        private void UpdateVisuals()
        {
            UpdatePosition();
            UpdateDeviceAddress();
            UpdateDeviceType();
            UpdateStatusColor();
            UpdateBlinking();
        }

        private void UpdatePosition()
        {
            if (PositionText != null)
            {
                PositionText.Text = $"{Math.Round(Position, 1)}%";
            }
        }

        private void UpdateDeviceAddress()
        {
            if (AddressText != null)
            {
                AddressText.Text = DeviceAddress.ToString();
            }
        }

        private void UpdateDeviceType()
        {
            if (AddressBox != null && AddressText != null)
            {
                // Light Blue (S/X), Orange (Nova), Purple (EH), or No Color
                switch (DeviceType?.ToUpper())
                {
                    case "S/X":
                    case "S7X":
                        AddressBox.Background = new SolidColorBrush(Color.FromRgb(173, 216, 230)); // Light Blue
                        AddressText.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // Dark text
                        break;
                    case "NOVA":
                        AddressBox.Background = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
                        AddressText.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // Dark text
                        break;
                    case "EH":
                        AddressBox.Background = new SolidColorBrush(Color.FromRgb(128, 0, 128)); // Purple
                        AddressText.Foreground = Brushes.White; // White text for better contrast
                        break;
                    default:
                        AddressBox.Background = Brushes.White;
                        AddressText.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // Dark text
                        break;
                }
            }
        }

        private void UpdateStatusColor()
        {
            if (TopRectangleFill != null)
            {
                TopRectangleFill.Color = StatusColor;
            }
            if (CircleFill != null)
            {
                CircleFill.Color = StatusColor;
            }
            if (ConnectorFill != null)
            {
                ConnectorFill.Color = StatusColor;
            }
        }

        private void UpdateBlinking()
        {
            if (this.Resources["BlinkAnimation"] is Storyboard blinkStoryboard)
            {
                if (IsBlinking)
                {
                    blinkStoryboard.Begin();
                }
                else
                {
                    blinkStoryboard.Stop();
                    // Reset to solid color
                    UpdateStatusColor();
                }
            }
        }

        // Helper method to get color from status
        public static Color GetColorFromStatus(string status)
        {
            switch (status?.ToLower())
            {
                case "open":
                case "active":
                case "ok":
                    return Colors.Green;
                case "closed":
                case "error":
                case "alarm":
                    return Colors.Red;
                case "opening":
                case "closing":
                case "moving":
                case "warning":
                    return Colors.Yellow;
                default:
                    return Colors.Transparent;
            }
        }
    }
}
