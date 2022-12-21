using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            typeof(IEnumerable<string>),
            typeof(StringsListBox));

        public IEnumerable<string> ItemsSource
        {
            get { return (IEnumerable<string>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        public StringsListBox()
        {
            InitializeComponent();
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            ItemsSource = ItemsSource.Append(string.Empty).ToArray();
            ItemsBox.Children.Add(new ContentPresenter
            {
                ContentTemplate = Resources["ItemTemplate"] as DataTemplate
            });
        }

        private void OnClearAllClick(object sender, RoutedEventArgs e)
        {
            ItemsSource = Array.Empty<string>();
            ItemsBox.Children.Clear();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var text = ((TextBox)sender).Text;
            var presenter = ((DependencyObject)sender).FindParent<ContentPresenter>()!;
            var index = presenter.FindParent<DependencyObject>()!.IndexOf(presenter);

            ItemsSource = ItemsSource.Take(index).Append(text).Concat(ItemsSource.Skip(index + 1)).ToArray();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var presenter = ((DependencyObject)sender).FindParent<ContentPresenter>()!;
            var index = presenter.FindParent<DependencyObject>()!.IndexOf(presenter);

            ItemsSource = ItemsSource.Take(index).Concat(ItemsSource.Skip(index + 1)).ToArray();
            ItemsBox.Children.RemoveAt(index);
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == ItemsSourceProperty)
            {
                if (e.OldValue == null)
                {
                    ItemsBox.Children.Clear();
                    foreach (var value in ItemsSource)
                    {
                        ItemsBox.Children.Add(new ContentPresenter
                        {
                            ContentTemplate = Resources["ItemTemplate"] as DataTemplate,
                            Content = new { Text = value },
                        });
                    }
                }
            }
        }
    }
}
