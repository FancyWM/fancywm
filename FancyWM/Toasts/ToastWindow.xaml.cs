using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;

using FancyWM.ViewModels;
using FancyWM.DllImports;

using WinMan;
using System.Windows.Interop;

namespace FancyWM.Toasts
{
    /// <summary>
    /// Interaction logic for ToastWindow.xaml
    /// </summary>
    public partial class ToastWindow : Window
    {
        public class ToastItem : ViewModelBase
        {
            public object Content { get; }

            public Action? ExtraAction { get; }

            public ToastItem(object content, Action? extraAction = null)
            {
                Content = content ?? throw new ArgumentNullException(nameof(content));
                ExtraAction = extraAction;
            }
        }

        public ObservableCollection<ToastItem> ToastItems { get; set; } = new ObservableCollection<ToastItem>();

        private readonly IWorkspace m_workspace;
        private IDisplay m_display;

        public ToastWindow(IWorkspace workspace)
        {
            m_workspace = workspace;

            InitializeComponent();
            DataContext = this;

            m_display = m_workspace.DisplayManager.PrimaryDisplay;
            UpdatePosition(m_display);
            m_workspace.DisplayManager.PrimaryDisplayChanged += OnPrimaryDisplayChanged;
            m_display.ScalingChanged += OnDisplayScalingChanged;

            var hwnd = new HWND(new WindowInteropHelper(this).EnsureHandle());
            _ = PInvoke.SetWindowLong(hwnd, GetWindowLongPtr_nIndex.GWL_STYLE, PInvoke.GetWindowLong(hwnd, GetWindowLongPtr_nIndex.GWL_STYLE) | (int)WINDOWS_STYLE.WS_DISABLED);
            _ = PInvoke.SetWindowLong(hwnd, GetWindowLongPtr_nIndex.GWL_EXSTYLE,
                (int)(WINDOWS_EX_STYLE.WS_EX_TOOLWINDOW | WINDOWS_EX_STYLE.WS_EX_TOPMOST | WINDOWS_EX_STYLE.WS_EX_NOACTIVATE));
            Show();
        }

        private void OnPrimaryDisplayChanged(object? sender, PrimaryDisplayChangedEventArgs e)
        {
            m_display.ScalingChanged -= OnDisplayScalingChanged;
            m_display = e.NewPrimaryDisplay;
            Dispatcher.Invoke(() =>
            {
                UpdatePosition(e.NewPrimaryDisplay);
            });
        }

        private void OnDisplayScalingChanged(object? sender, DisplayScalingChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdatePosition(m_display);
            });
        }

        private void UpdatePosition(IDisplay display)
        {
            var workArea = display.WorkArea;
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            PInvoke.SetWindowPos(new(hwnd), new(), workArea.Left, workArea.Top, workArea.Width, workArea.Height,
                SetWindowPos_uFlags.SWP_NOZORDER | SetWindowPos_uFlags.SWP_NOACTIVATE);
        }

        internal void ShowToast(object content, CancellationToken token)
        {
            var item = new ToastItem(content);
            ToastItems.Clear();
            ToastItems.Add(item);
            PInvoke.SetWindowPos(new(new WindowInteropHelper(this).EnsureHandle()), new(-1), 0, 0, 0, 0, SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
            token.Register(() => Dispatcher.Invoke(() => ToastItems.Remove(item)));
        }

        internal void ShowToast(object content, Action extraAction, CancellationToken token)
        {
            var item = new ToastItem(content, extraAction);
            ToastItems.Clear();
            ToastItems.Add(item);
            PInvoke.SetWindowPos(new(new WindowInteropHelper(this).EnsureHandle()), new(-1), 0, 0, 0, 0, SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
            token.Register(() => Dispatcher.Invoke(() => ToastItems.Remove(item)));
        }
    }
}
