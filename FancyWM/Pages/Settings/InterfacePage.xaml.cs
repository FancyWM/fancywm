using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for InterfacePage.xaml
    /// </summary>
    public partial class InterfacePage : UserControl
    {
        public InterfacePage(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
