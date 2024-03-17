using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for ExclusionsPage.xaml
    /// </summary>
    public partial class RulesPage : UserControl
    {
        private readonly SettingsViewModel m_viewModel;

        public RulesPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            m_viewModel = viewModel;
            DataContext = viewModel;
        }
    }
}
