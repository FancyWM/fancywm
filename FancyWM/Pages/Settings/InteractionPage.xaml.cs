using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for InteractionPage.xaml
    /// </summary>
    public partial class InteractionPage : UserControl
    {
        public InteractionPage(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
