using System;
using System.Collections.Generic;
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

        public TilingWindow()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ViewModelProperty)
            {
                DataContext = ViewModel;
                MoreContextMenu.IsOpen = false;
            }
        }

        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            MoreContextMenu.IsOpen = true;
            MoreContextMenu.DataContext = ViewModel;
            var child = (UIElement)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(MoreContextMenu, 0), 0);
            child.MouseEnter -= OnContextMenuMouseEnter;
            child.MouseEnter += OnContextMenuMouseEnter;
        }

        private void OnContextMenuMouseEnter(object sender, MouseEventArgs e)
        {
            var child = (UIElement)sender;
            child.MouseEnter -= OnContextMenuMouseEnter;
            child.MouseLeave -= OnContextMenuMouseLeave;
            child.MouseLeave += OnContextMenuMouseLeave;
        }

        private void OnContextMenuMouseLeave(object sender, MouseEventArgs e)
        {
            var child = (UIElement)sender;
            child.MouseLeave -= OnContextMenuMouseLeave;
            MoreContextMenu.IsOpen = false;
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
                    UnregisterCapture(btn);
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
                    UnregisterCapture(btn);
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
                    UnregisterCapture(btn);
                }
            }
            else
            {
                m_canTriggerStackGroup = true;
            }
        }

        private void UnregisterCapture(Button btn)
        {
            void onCaptureLost(object sender, MouseEventArgs e)
            {
                ViewModel.IsActionActive = false;
                btn.LostMouseCapture -= onCaptureLost;
            }
            btn.LostMouseCapture += onCaptureLost;
        }
    }
}
