using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;

using FancyWM.ViewModels;

using Windows.ApplicationModel;
using Windows.System;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for AboutPage.xaml
    /// </summary>
    public partial class AboutPage : UserControl
    {
        public AboutPage()
        {
            InitializeComponent();
            DataContext = new
            {
                AppVersionText = App.Current.VersionString,
            };
        }

        public AboutPage(SettingsViewModel _) : this()
        {
        }

        private void OnHyperlinkRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            _ = Launcher.LaunchUriAsync(e.Uri);
        }
    }
}
