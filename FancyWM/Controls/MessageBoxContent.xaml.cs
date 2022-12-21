using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for MessageBoxContent.xaml
    /// </summary>
    public partial class MessageBoxContent : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(MessageBoxContent),
            new PropertyMetadata(null));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty HintTextProperty = DependencyProperty.Register(
            nameof(HintText),
            typeof(string),
            typeof(MessageBoxContent),
            new PropertyMetadata(null));

        public string? HintText
        {
            get => (string?)GetValue(HintTextProperty);
            set => SetValue(HintTextProperty, value);
        }

        public static readonly DependencyProperty HintTextVisibilityProperty = DependencyProperty.Register(
            nameof(HintTextVisibility),
            typeof(Visibility),
            typeof(MessageBoxContent),
            new PropertyMetadata(null));

        public Visibility HintTextVisibility
        {
            get => (Visibility)GetValue(HintTextVisibilityProperty);
            set => SetValue(HintTextVisibilityProperty, value);
        }

        public MessageBoxContent()
        {
            InitializeComponent();
            DataContext = this;
            HintTextVisibility = Visibility.Collapsed;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == HintTextProperty)
            {
                HintTextVisibility = string.IsNullOrWhiteSpace(HintText) ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}
