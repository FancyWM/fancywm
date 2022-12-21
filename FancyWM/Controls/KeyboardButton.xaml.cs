using System.Windows;
using System.Windows.Controls;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for KeyboardButton.xaml
    /// </summary>
    public partial class KeyboardButton : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(KeyboardButton),
            new PropertyMetadata(null));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public KeyboardButton()
        {
            InitializeComponent();
            DataContext = this;
        }
    }
}
