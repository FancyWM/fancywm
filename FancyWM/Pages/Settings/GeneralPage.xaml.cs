using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for GeneralPage.xaml
    /// </summary>
    public partial class GeneralPage : UserControl
    {
        private readonly SettingsViewModel m_viewModel;

        public GeneralPage(SettingsViewModel viewModel)
        {
            m_viewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();
        }

        private void OpenInExplorerButtonClick(object sender, RoutedEventArgs e)
        {
            var path = App.Current.GetRealPath(m_viewModel.Model.FullPath);
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
    }
}
