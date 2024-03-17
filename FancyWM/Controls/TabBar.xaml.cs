using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for TabBar.xaml
    /// </summary>
    public partial class TabBar : UserControl
    {
        public static DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), 
            typeof(ObservableCollection<TilingNodeViewModel>), 
            typeof(TabBar));

        public ObservableCollection<TilingNodeViewModel> ItemsSource
        {
            get => (ObservableCollection<TilingNodeViewModel>)GetValue(ItemsSourceProperty); 
            set => SetValue(ItemsSourceProperty, value); 
        }

        public static DependencyProperty TabMinWidthProperty = DependencyProperty.Register(
            nameof(TabMinWidth),
            typeof(int),
            typeof(TabBar));

        public int TabMinWidth
        {
            get => (int)GetValue(TabMinWidthProperty);
            set => SetValue(TabMinWidthProperty, value);
        }

        public static DependencyProperty TabMaxWidthProperty = DependencyProperty.Register(
            nameof(TabMaxWidth),
            typeof(int),
            typeof(TabBar));

        public int TabMaxWidth
        {
            get => (int)GetValue(TabMaxWidthProperty);
            set => SetValue(TabMaxWidthProperty, value);
        }

        private Grid? m_grid = null;

        public TabBar()
        {
            InitializeComponent();
        }

        private void OnGridLoaded(object sender, RoutedEventArgs e)
        {
            m_grid = (Grid)sender;
            UpdateGrid();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.Property == ItemsSourceProperty)
            {
                if (e.OldValue is ObservableCollection<TilingNodeViewModel> oldCollection)
                {
                    oldCollection.CollectionChanged -= OnItemsSourceChanged;
                }
                if (e.NewValue is ObservableCollection<TilingNodeViewModel> newCollection)
                {
                    newCollection.CollectionChanged += OnItemsSourceChanged;
                }
                UpdateGrid();
            }
            else if (e.Property == TabMinWidthProperty || e.Property == TabMaxWidthProperty)
            {
                UpdateGrid();
            }
        }

        private void UpdateGrid()
        {
            if (m_grid == null)
                return;

            while (m_grid.ColumnDefinitions.Count < ItemsSource.Count)
            {
                m_grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    MinWidth = TabMinWidth,
                    MaxWidth = TabMaxWidth,
                    Width = new GridLength(1, GridUnitType.Star),
                });
            }
            while (m_grid.ColumnDefinitions.Count > ItemsSource.Count)
            {
                m_grid.ColumnDefinitions.RemoveAt(0);
            }
            foreach (var column in m_grid.ColumnDefinitions)
            {
                column.MinWidth = TabMinWidth;
                column.MaxWidth = TabMaxWidth;
            }
            for (int i = 0; i < m_grid.Children.Count; i++)
            {
                Grid.SetColumn(m_grid.Children[i], i);
                Grid.SetRow(m_grid.Children[i], 0);
            }
        }

        private void OnItemsSourceChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateGrid();
        }
    }
}
