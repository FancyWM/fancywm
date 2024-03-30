using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using FancyWM.Utilities;

using WinMan;
using FancyWM.Layouts.Tiling;
using System.Windows.Media.Imaging;

namespace FancyWM.ViewModels
{
    public abstract class TilingNodeViewModel : ViewModelBase
    {
        private TilingNode? m_node;
        private bool m_hasFocus;
        private Rectangle m_windowBounds;
        private ICommand? m_primaryActionCommand;
        private ICommand? m_secondaryActionCommand;
        private ICommand? m_closeActionCommand;

        public event RoutedEventHandler? HorizontalSplitActionPressed;
        public event RoutedEventHandler? VerticalSplitActionPressed;
        public event RoutedEventHandler? StackActionPressed;
        public event RoutedEventHandler? PullUpActionPressed;

        public ICommand HorizontalSplitCommand { get; }
        public ICommand VerticalSplitCommand { get; }
        public ICommand StackCommand { get; }
        public ICommand PullUpCommand { get; }

        public ImageSource? Icon => GetCachedImageSource();

        public Visibility IconVisibility => Icon != null ? Visibility.Visible : Visibility.Collapsed;

        protected TilingNodeViewModel()
        {
            HorizontalSplitCommand = new DelegateCommand(_ => HorizontalSplitActionPressed?.Invoke(this, new RoutedEventArgs()));
            VerticalSplitCommand = new DelegateCommand(_ => VerticalSplitActionPressed?.Invoke(this, new RoutedEventArgs()));
            StackCommand = new DelegateCommand(_ => StackActionPressed?.Invoke(this, new RoutedEventArgs()));
            PullUpCommand = new DelegateCommand(_ => PullUpActionPressed?.Invoke(this, new RoutedEventArgs()));
        }

        public TilingNode? Node { get => m_node; set => SetField(ref m_node, value); }
        public bool HasFocus { get => m_hasFocus; set => SetField(ref m_hasFocus, value); }
        public Rectangle ComputedBounds { get => m_windowBounds; set => SetField(ref m_windowBounds, value); }

        public ICommand? PrimaryActionCommand { get => m_primaryActionCommand; set => SetField(ref m_primaryActionCommand, value); }

        public ICommand? SecondaryActionCommand { get => m_secondaryActionCommand; set => SetField(ref m_secondaryActionCommand, value); }

        public ICommand? CloseCommand { get => m_closeActionCommand; set => SetField(ref m_closeActionCommand, value); }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        public override void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            base.Dispose();
            HorizontalSplitActionPressed = null;
            VerticalSplitActionPressed = null;
            StackActionPressed = null;
            PullUpActionPressed = null;
            PrimaryActionCommand = null;
            SecondaryActionCommand = null;
            CloseCommand = null;
        }

        private BitmapSource? GetCachedImageSource()
        {
            if (m_node is not WindowNode windowNode)
            {
                return null;
            }

            var window = windowNode.WindowReference;
            return window.GetCachedIcon();
        }
    }

}
