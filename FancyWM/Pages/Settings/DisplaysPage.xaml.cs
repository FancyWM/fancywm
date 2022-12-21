using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for DisplaysPage.xaml
    /// </summary>
    public partial class DisplaysPage : UserControl
    {
        public DisplaysPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
