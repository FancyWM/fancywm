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
                AppVersionText = GetVersionString(),
            };
        }

        public AboutPage(SettingsViewModel _) : this()
        {
        }

        private void OnHyperlinkRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            _ = Launcher.LaunchUriAsync(e.Uri);
        }

        private string GetVersionString()
        {
            try
            {
                Package package = Package.Current;
                PackageId packageId = package.Id;
                PackageVersion version = packageId.Version;
                return string.Format("{0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            }
            catch (Exception)
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location);
                return versionInfo.FileVersion ?? "0.0.0.0";
            }
        }
    }
}
