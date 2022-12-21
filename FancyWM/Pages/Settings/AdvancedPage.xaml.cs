using System;
using System.Reflection;
using System.Windows.Controls;

using FancyWM.Resources;
using FancyWM.ViewModels;
using FancyWM.Windows;

using Microsoft.Win32;

using Windows.ApplicationModel;
using Windows.System;

namespace FancyWM.Pages.Settings
{
    /// <summary>
    /// Interaction logic for AdvancedPage.xaml
    /// </summary>
    public partial class AdvancedPage : UserControl
    {
        public AdvancedPage()
        {
            InitializeComponent();
            DataContext = new
            {
                AppVersionText = GetVersionString(),
            };
        }

        public AdvancedPage(SettingsViewModel _) : this()
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
                return Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0.0";
            }
        }

        private async void CreateAhkScriptClick(object sender, System.Windows.RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                AddExtension = true,
                CheckFileExists = false,
                CreatePrompt = false,
                Filter = "AutoHotkey Script (*.ahk)|*.ahk"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                using var file = saveFileDialog.OpenFile();
                await file.WriteAsync(Files.FancyWM_ahk);

                if (sender is Button btn)
                {
                    if (btn.Content is string content)
                    {
                        btn.Content = content + " ✓";
                    }
                }
            }
        }
    }
}
