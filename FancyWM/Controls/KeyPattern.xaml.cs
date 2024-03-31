using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

        public static readonly DependencyProperty KeyStringsProperty = DependencyProperty.Register(
            nameof(KeyStrings),
            typeof(List<string>),
            typeof(KeyPattern),
            new PropertyMetadata(new List<string>()));

        public IReadOnlySet<KeyCode> Pattern
        {
            get => (IReadOnlySet<KeyCode>)GetValue(PatternProperty);
            set => SetValue(PatternProperty, value);
        }

        public List<string> KeyStrings
        {
            get => (List<string>)GetValue(KeyStringsProperty);
            set => SetValue(KeyStringsProperty, value);
        }

        public KeyPattern()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == PatternProperty && Pattern != null)
            {
                UpdateKeyStrings();
            }
        }

        private void UpdateKeyStrings()
        {
            if (Pattern != null)
            {
                KeyStrings = [.. Pattern.Select(KeyDescriptions.GetDescription)];
            }
        }
    }
}
