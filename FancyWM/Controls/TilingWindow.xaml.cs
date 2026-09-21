using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using FancyWM.Layouts.Tiling;
using FancyWM.ViewModels;

namespace FancyWM.Controls
{
    /// <summary>
    /// Interaction logic for TilingWindow.xaml
    /// </summary>
    public partial class TilingWindow : UserControl
    {

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(TilingWindowViewModel),
            typeof(TilingWindow),
            new PropertyMetadata(null));

        public TilingWindowViewModel ViewModel
        {
            get => (TilingWindowViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        // The overlay window never activates, so the menu gets no deactivation
        // to close itself on. Close it once the cursor has left it instead.
        private const int MenuCloseAfterMissedTicks = 2;
        private readonly DispatcherTimer m_menuWatchTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
        private int m_menuMissedTicks;

        public TilingWindow()
        {
            InitializeComponent();
            m_menuWatchTimer.Tick += OnMenuWatchTick;
            MoreContextMenu.Opened += (_, _) => SetMenuOpen(ViewModel, true);
            MoreContextMenu.Closed += (_, _) =>
            {
                m_menuWatchTimer.Stop();
                SetMenuOpen(ViewModel, false);
            };
            Unloaded += (_, _) => MoreContextMenu.IsOpen = false;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ViewModelProperty)
            {
                DataContext = ViewModel;
                MoreContextMenu.IsOpen = false;
                SetMenuOpen(e.OldValue as TilingWindowViewModel, false);
            }
        }

        private static void SetMenuOpen(TilingWindowViewModel? viewModel, bool isOpen)
        {
            if (viewModel != null)
            {
                viewModel.IsMenuOpen = isOpen;
            }
        }

        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            MoreContextMenu.IsOpen = true;
            MoreContextMenu.DataContext = ViewModel;
            m_menuMissedTicks = 0;
            m_menuWatchTimer.Start();
        }

        private void OnMenuWatchTick(object? sender, EventArgs e)
        {
            if (IsCursorOver(MoreContextMenu) || IsCursorOver(MoreButton))
            {
                m_menuMissedTicks = 0;
                return;
            }

            if (++m_menuMissedTicks >= MenuCloseAfterMissedTicks)
            {
                MoreContextMenu.IsOpen = false;
            }
        }

        /// <summary>
        /// IsMouseOver is useless here: the open menu captures the mouse,
        /// and WPF reports the capturing element as the one under the cursor.
        /// </summary>
        private bool IsCursorOver(FrameworkElement element)
        {
            if (ViewModel?.Node is not WindowNode node || PresentationSource.FromVisual(element) == null)
            {
                return false;
            }

            var cursor = node.WindowReference.Workspace.CursorLocation;
            var local = element.PointFromScreen(new Point(cursor.X, cursor.Y));
            return 0 <= local.X && local.X <= element.ActualWidth
                && 0 <= local.Y && local.Y <= element.ActualHeight;
        }

        bool m_canTriggerHorizontalGroup = true;
        Point m_horizontalLastPosition = default;

        private void OnHorizontalSplitMouseMove(object sender, MouseEventArgs e)
        {
            var btn = (Button)sender;
            // Has it really moved?
            var p = e.GetPosition(btn);
            if (m_horizontalLastPosition != p)
            {
                m_horizontalLastPosition = p;
            }
            else return;

            if (m_canTriggerHorizontalGroup && e.MouseDevice.LeftButton == MouseButtonState.Pressed && btn.IsMouseCaptured)
            {
                if (!ViewModel.IsActionActive)
                {
                    ViewModel.IsActionActive = true;
                    ViewModel.BeginHorizontalSplitWithCommand.Execute(null);
                    m_canTriggerHorizontalGroup = false;
                }
            }
            else
            {
                m_canTriggerHorizontalGroup = true;
            }
        }

        bool m_canTriggerVerticalGroup = true;
        Point m_verticalLastPosition = default;

        private void OnVerticalSplitMouseMove(object sender, MouseEventArgs e)
        {
            var btn = (Button)sender;
            // Has it really moved?
            var p = e.GetPosition(btn);
            if (m_verticalLastPosition != p)
            {
                m_verticalLastPosition = p;
            }
            else return;

            if (m_canTriggerVerticalGroup && e.MouseDevice.LeftButton == MouseButtonState.Pressed && btn.IsMouseCaptured)
            {
                if (!ViewModel.IsActionActive)
                {
                    ViewModel.IsActionActive = true;
                    ViewModel.BeginVerticalSplitWithCommand.Execute(null);
                    m_canTriggerVerticalGroup = false;
                }
            }
            else
            {
                m_canTriggerVerticalGroup = true;
            }
        }

        bool m_canTriggerStackGroup = true;
        Point m_stackLastPosition = default;

        private void OnStackMouseMove(object sender, MouseEventArgs e)
        {
            var btn = (Button)sender;
            // Has it really moved?
            var p = e.GetPosition(btn);
            if (m_stackLastPosition != p)
            {
                m_stackLastPosition = p;
            }
            else return;

            if (m_canTriggerStackGroup && Mouse.LeftButton == MouseButtonState.Pressed && btn.IsMouseCaptured)
            {
                if (!ViewModel.IsActionActive)
                {
                    ViewModel.IsActionActive = true;
                    ViewModel.BeginStackWithCommand.Execute(null);
                    m_canTriggerStackGroup = false;
                }
            }
            else
            {
                m_canTriggerStackGroup = true;
            }
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (ViewModel.IsActionActive)
            {
                ViewModel.IsActionActive = false;
            }
        }
    }
}
