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

        public static readonly DependencyProperty ClosePositionProperty =
            DependencyProperty.Register("ClosePosition", typeof(double), typeof(BrayValveControl),
                new PropertyMetadata(0.0, OnClosePositionChanged));

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

        public double ClosePosition
        {
            get { return (double)GetValue(ClosePositionProperty); }
            set { SetValue(ClosePositionProperty, value); }
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

        private static void OnClosePositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (BrayValveControl)d;
            control.UpdateClosePosition();
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
            UpdateClosePosition();
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

        private void UpdateClosePosition()
        {
            if (TopPercentage != null)
            {
                TopPercentage.Text = $"{Math.Round(ClosePosition, 0)}%";
            }
        }

        private void UpdateDeviceAddress()
        {
            if (NumberText != null)
            {
                NumberText.Text = DeviceAddress.ToString();
            }
        }

        private void UpdateDeviceType()
        {
            if (BottomBox != null)
            {
                // Light Blue (S/X), Orange (Nova), Purple (EH), or No Color
                switch (DeviceType?.ToUpper())
                {
                    case "S/X":
                    case "S7X":
                        BottomBox.Background = new SolidColorBrush(Color.FromRgb(173, 216, 230)); // Light Blue
                        break;
                    case "NOVA":
                        BottomBox.Background = new SolidColorBrush(Color.FromRgb(255, 165, 0)); // Orange
                        break;
                    case "EH":
                        BottomBox.Background = new SolidColorBrush(Color.FromRgb(128, 0, 128)); // Purple
                        NumberText.Foreground = Brushes.White; // White text for better contrast
                        break;
                    default:
                        BottomBox.Background = Brushes.White;
                        break;
                }
            }
        }

        private void UpdateStatusColor()
        {
            if (CenterCircle != null)
            {
                CenterCircle.Fill = new SolidColorBrush(StatusColor);
            }

            if (ValveColorBorder != null && !IsBlinking)
            {
                ValveColorBorder.BorderBrush = new SolidColorBrush(StatusColor);
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
                    if (ValveColorBorder != null)
                    {
                        ValveColorBorder.BorderBrush = new SolidColorBrush(StatusColor);
                    }
                    if (CenterCircle != null)
                    {
                        CenterCircle.Fill = new SolidColorBrush(StatusColor);
                    }
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
