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
        }

        private void sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sidebar.SelectedItem is NavButton navButton && navButton.Navlink != null)
            {
                navframe.Source = navButton.Navlink;
            }
        }
    }
}