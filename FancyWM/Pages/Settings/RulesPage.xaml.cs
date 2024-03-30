using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for ExclusionsPage.xaml
    /// </summary>
    public partial class RulesPage : UserControl
    {
        public RulesPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
