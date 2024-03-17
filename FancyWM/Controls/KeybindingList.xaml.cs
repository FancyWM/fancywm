using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using FancyWM.Models;
using FancyWM.Utilities;
using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for BoundActionList.xaml
    /// </summary>
    public partial class KeybindingList : UserControl
    {
        public class GridElement
        {
            public KeybindingViewModel Element { get; set; }
            public int RowIndex { get; set; }
            public int ColumnIndex { get; set; }
        }

        private static readonly BindableAction[] Ordering = new BindableAction[]
        {
            BindableAction.CreateHorizontalPanel,
            BindableAction.CreateVerticalPanel,
            BindableAction.CreateStackPanel,
            BindableAction.PullWindowUp,

            BindableAction.MoveFocusLeft,
            BindableAction.MoveFocusUp,
            BindableAction.MoveFocusRight,
            BindableAction.MoveFocusDown,

            BindableAction.MoveLeft,
            BindableAction.MoveUp,
            BindableAction.MoveRight,
            BindableAction.MoveDown,

            BindableAction.SwapLeft,
            BindableAction.SwapUp,
            BindableAction.SwapRight,
            BindableAction.SwapDown,

            BindableAction.IncreaseWidth,
            BindableAction.IncreaseHeight,
            BindableAction.DecreaseWidth,
            BindableAction.DecreaseHeight,

            BindableAction.SwitchToPreviousDesktop,
            BindableAction.MoveToPreviousDesktop,

            BindableAction.SwitchToPreviousDisplay,
            BindableAction.MoveToPreviousDisplay,

            BindableAction.ShowDesktop,
            BindableAction.RefreshWorkspace,
            BindableAction.ToggleFloatingMode,
            BindableAction.ToggleManager,
            BindableAction.Cancel,
        };

        public static readonly DependencyProperty KeybindingsProperty = DependencyProperty.Register(
            nameof(Keybindings),
            typeof(KeybindingDictionary),
            typeof(KeybindingList),
            new PropertyMetadata(null));

        public KeybindingDictionary Keybindings
        {
            get => (KeybindingDictionary)GetValue(KeybindingsProperty);
            set
            {
                SetValue(KeybindingsProperty, value);
            }
        }

        public KeybindingList()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == KeybindingsProperty)
            {
                UpdateDataContext();
            }
        }

        private void UpdateDataContext()
        {
            DataContext = new
            {
                Keybindings = CreateGrid(KeybindingViewModel.FromDictionary(Keybindings).OrderBy(x => Ordering.IndexOf(x.Action)).ToList()).ToList(),
            };
        }

        private static IList<GridElement> CreateGrid(IList<KeybindingViewModel> list)
        {
            var grid = new List<GridElement>();
            for (int i = 0; i < list.Count; i++)
            {
                var halfCount = (list.Count + 1) / 2;
                grid.Add(new GridElement
                {
                    Element = list[i],
                    RowIndex = i % halfCount,
                    ColumnIndex = i >= halfCount ? 1 : 0,
                });
            }
            return grid;
        }
    }
}
