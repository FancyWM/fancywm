using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using FancyWM.Utilities;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for EditableListBox.xaml
    /// </summary>
    public partial class StringsListBox : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IList<string>),
            typeof(StringsListBox));

        public IList<string> ItemsSource
        {
            get { return (IList<string>)GetValue(ItemsSourceProperty); }
            set { SetCurrentValue(ItemsSourceProperty, value); }
        }

        public StringsListBox()
        {
            InitializeComponent();
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            ItemsSource = ItemsSource.Append(string.Empty).ToArray();
        }

        private void OnClearAllClick(object sender, RoutedEventArgs e)
        {
            ItemsSource = [];
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text = ((TextBox)sender).Text;
            var presenter = ((DependencyObject)sender).FindParent<ContentPresenter>()!;
            var parent = presenter.FindParent<DependencyObject>();
            if (parent == null)
            {
                return;
            }

            var index = parent.IndexOf(presenter);
            ItemsSource = ItemsSource.Take(index).Append(text).Concat(ItemsSource.Skip(index + 1)).ToArray();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var presenter = ((DependencyObject)sender).FindParent<ContentPresenter>()!;
            var parent = presenter.FindParent<DependencyObject>();
            if (parent == null)
            {
                return;
            }

            var index = parent.IndexOf(presenter);
            ItemsSource = ItemsSource.Take(index).Concat(ItemsSource.Skip(index + 1)).ToArray();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == ItemsSourceProperty)
            {
                ItemsBox.Children.Clear();
                foreach (var value in ItemsSource ?? [])
                {
                    ItemsBox.Children.Add(new ContentPresenter
                    {
                        ContentTemplate = Resources["ItemTemplate"] as DataTemplate,
                        Content = new { Text = value },
                        Tag = value
                    });
                }
            }
        }
    }
}
