using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPF_GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Navigate to the first page after the window is loaded
            if (navframe != null)
            {
                navframe.Source = new Uri("/NewFolder/Page1.xaml", UriKind.Relative);
            }
        }

        private void NavButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag != null && navframe != null)
            {
                string? page = radioButton.Tag.ToString();
                if (!string.IsNullOrEmpty(page))
                {
                    navframe.Source = new Uri(page, UriKind.Relative);
                }
            }
        }

        private void ModeChanged(object sender, RoutedEventArgs e)
        {
            if (SimulationModeRadio == null || ProductionModeRadio == null)
                return;

            bool isSimulation = SimulationModeRadio.IsChecked == true;
            App.SetMode(isSimulation);

            // Show notification
            string modeText = isSimulation ? "Simulation" : "Production";
            MessageBox.Show($"Switched to {modeText} mode", "Mode Changed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}