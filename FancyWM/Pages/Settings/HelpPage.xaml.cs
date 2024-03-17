using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using FancyWM.ViewModels;

using Windows.System;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for HelpPage.xaml
    /// </summary>
    public partial class HelpPage : UserControl
    {
        public HelpPage(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }

        private void OpenUrl(object sender, RoutedEventArgs e)
        {
            var hyperlink = (Hyperlink)sender;
            _ = Launcher.LaunchUriAsync(hyperlink.NavigateUri);
        }

        private void OpenDataDir(object sender, RoutedEventArgs e)
        {
            _ = Launcher.LaunchUriAsync(new Uri(Directory.GetCurrentDirectory()));
        }
    }
}
