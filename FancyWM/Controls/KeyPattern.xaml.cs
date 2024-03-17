using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using FancyWM.Utilities;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for KeyPattern.xaml
    /// </summary>
    public partial class KeyPattern : UserControl
    {
        public static readonly DependencyProperty PatternProperty = DependencyProperty.Register(
            nameof(Pattern),
            typeof(IReadOnlySet<KeyCode>),
            typeof(KeyPattern),
            new PropertyMetadata(null));

        public IReadOnlySet<KeyCode> Pattern
        {
            get => (IReadOnlySet<KeyCode>)GetValue(PatternProperty);
            set => SetValue(PatternProperty, value);
        }

        public KeyPattern()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == PatternProperty)
            {
                if (Pattern != null)
                    UpdateDataContext();
            }
        }

        private void UpdateDataContext()
        {
            DataContext = new
            {
                Pattern = Pattern?.Select(x => KeyDescriptions.GetDescription(x))?.ToList()
            };
        }
    }
}
