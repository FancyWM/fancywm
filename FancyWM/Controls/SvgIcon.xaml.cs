using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for SvgIcon.xaml
    /// </summary>
    public partial class SvgIcon : UserControl
    {
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(string),
            typeof(SvgIcon),
            new PropertyMetadata(null));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
            nameof(Color),
            typeof(Brush),
            typeof(SvgIcon),
            new PropertyMetadata(Brushes.White));

        public Brush Color
        {
            get => (Brush)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public SvgIcon()
        {
            InitializeComponent();
            DataContext = this;
        }
    }
}
