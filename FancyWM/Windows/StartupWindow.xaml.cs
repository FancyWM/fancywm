using System.Windows;

using FancyWM.ViewModels;

namespace FancyWM.Windows
{
    /// <summary>
    /// Interaction logic for StartupWindow.xaml
    /// </summary>
    public partial class StartupWindow : Window
    {
        private readonly SettingsViewModel m_settingsViewModel;

        public StartupWindow(SettingsViewModel settingsViewModel)
        {
            InitializeComponent();
            m_settingsViewModel = settingsViewModel;
            DataContext = m_settingsViewModel;
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Close();
            MainWindow.OpenSettings();
        }
    }
}
