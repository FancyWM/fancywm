using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for KeybindingsPage.xaml
    /// </summary>
    public partial class KeybindingsPage : UserControl
    {
        public KeybindingsPage(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
