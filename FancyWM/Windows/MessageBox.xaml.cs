using System;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;

using FancyWM.Utilities;

using Microsoft.Extensions.DependencyInjection;

using Windows.System;

namespace FancyWM.Windows
{
    /// <summary>
    /// Interaction logic for MessageBox.xaml
    /// </summary>
    public partial class MessageBox : Window
    {
        public static readonly DependencyProperty IconGlyphProperty = DependencyProperty.Register(
            nameof(IconGlyph),
            typeof(string),
            typeof(MessageBox),
            new PropertyMetadata(null));

        public string IconGlyph
        {
            get => (string)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
            nameof(Message),
            typeof(object),
            typeof(MessageBox),
            new PropertyMetadata(null));

        public object Message
        {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public MessageBox()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void OnPositiveButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void OnNegativeButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
