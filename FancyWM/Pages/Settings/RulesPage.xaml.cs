using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
