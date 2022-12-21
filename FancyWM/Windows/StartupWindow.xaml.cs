using System.Windows;

using FancyWM.ViewModels;

namespace FancyWM.Windows
{
    /// <summary>
    /// Interaction logic for StartupWindow.xaml
    /// </summary>
    public partial class StartupWindow : Window
    {
        private readonly MainWindow m_mainWindow;
        private readonly SettingsViewModel m_settingsViewModel;

        public StartupWindow(MainWindow mainWindow, SettingsViewModel settingsViewModel)
        {
            InitializeComponent();
            m_mainWindow = mainWindow;
            m_settingsViewModel = settingsViewModel;
            DataContext = m_settingsViewModel;
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Close();
            m_mainWindow.OpenSettings();
        }
    }
}
