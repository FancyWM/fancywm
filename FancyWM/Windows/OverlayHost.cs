using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

using WinMan;

using FancyWM.DllImports;
using FancyWM.ViewModels;
using System.Reactive.Linq;
using System.Windows.Input;
using FancyWM.Utilities;

namespace FancyWM.Windows
{
    /// <summary>
    /// This is basically a workaround for showing overlay content that is ignored by the operating system.
    /// It uses two identially-positioned windows, one with WS_EX_TRANSPARENT and one without.
    /// </summary>
    class OverlayHost : DependencyObject
    {
        [Flags()]
        internal enum SetWindowPosFlags : uint
        {
            IgnoreResize = 0x0001,
            IgnoreMove = 0x0002,
            DoNotActivate = 0x0010,
            DoNotSendChangingEvent = 0x0400,
            AsynchronousWindowPosition = 0x4000,
        }

        private class OverlayWindow : Window
        {
            private readonly IDisplay m_display;
            private readonly Border m_contentContainer;
            private readonly IDisposable m_subscription;
            private int m_panelFontSize;
            private HWND m_hwnd;

            public bool AllowClose { get; set; } = false;

            public OverlayWindow(IDisplay display)
            {
                m_display = display;
                var bounds = display.Bounds;

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                Topmost = true;
                ShowInTaskbar = false;
                AllowsTransparency = true;
                Background = null;

                m_contentContainer = new Border();
                Content = m_contentContainer;
                UpdatePosition();

                m_display.WorkAreaChanged += OnDisplayWorkAreaChanged;
                m_display.ScalingChanged += OnDisplayScalingChanged;

                m_subscription = App.Current.AppState.Settings
                    .Select(x => x.PanelFontSize)
                    .DistinctUntilChanged()
                    .Subscribe(x =>
                    {
                        m_panelFontSize = x;
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            Resources["DisplayScaling"] = m_display.Scaling;
                            Resources["OverlayFontSize"] = m_panelFontSize * m_display.Scaling;
                        });
                    });
            }

            protected override void OnClosing(CancelEventArgs e)
            {
                if (!AllowClose)
                {
                    e.Cancel = true;
                }
            }

            internal void SetContent(Control content)
            {
                m_contentContainer.Child = content;
            }

            private void UpdatePosition()
            {
                var bounds = m_display.Bounds;
                var workArea = m_display.WorkArea;
                var tg = new TransformGroup();
                tg.Children.Add(new ScaleTransform(
                        1 / m_display.Scaling,
                        1 / m_display.Scaling));
                tg.Children.Add(new TranslateTransform(
                    bounds.Left - workArea.Left,
                    bounds.Top - workArea.Top));

                m_contentContainer.RenderTransform = tg;

                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                PInvoke.SetWindowPos(new(hwnd), new(), workArea.Left, workArea.Top, workArea.Width, workArea.Height,
                    SetWindowPos_uFlags.SWP_NOZORDER | SetWindowPos_uFlags.SWP_NOACTIVATE);
            }

            private void OnDisplayScalingChanged(object? sender, DisplayScalingChangedEventArgs e)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdatePosition();
                    Resources["DisplayScaling"] = m_display.Scaling;
                    Resources["OverlayFontSize"] = m_panelFontSize * m_display.Scaling;
                });
            }

            private void OnDisplayWorkAreaChanged(object? sender, DisplayRectangleChangedEventArgs e)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdatePosition();
                });
            }

            protected override void OnClosed(EventArgs e)
            {
                base.OnClosed(e);
                m_subscription?.Dispose();
                m_display.WorkAreaChanged -= OnDisplayWorkAreaChanged;
                m_display.ScalingChanged -= OnDisplayScalingChanged;
                Content = null;
            }
        }

        public static readonly DependencyProperty NonHitTestableContentProperty = DependencyProperty.Register(
            nameof(NonHitTestableContent),
            typeof(Control),
            typeof(OverlayHost),
            new PropertyMetadata(null));

        public Control? NonHitTestableContent
        {
            get => (Control)GetValue(NonHitTestableContentProperty);
            set => SetValue(NonHitTestableContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
            nameof(Content),
            typeof(Control),
            typeof(OverlayHost),
            new PropertyMetadata(null));

        public Control? Content
        {
            get => (Control)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public Func<IntPtr>? AnchorSource { get; set; }

        private readonly IDisplay m_display;
        private readonly OverlayWindow m_window;
        private readonly OverlayWindow m_nonHitTestableWindow;
        private readonly IntPtr m_hwnd;
        private readonly IntPtr m_nonHitTestableHwnd;
        private bool m_isShown;
        private bool m_isClosed;

        public OverlayHost(IDisplay display)
        {
            m_display = display;

            m_window = new OverlayWindow(display)
            {
                Title = "FancyWMInteractiveOverlay",
            };
            m_nonHitTestableWindow = new OverlayWindow(display)
            {
                IsHitTestVisible = false,
                Title = "FancyWMNonInteractiveOverlay",
            };

            m_hwnd = new WindowInteropHelper(m_window).EnsureHandle();
            m_nonHitTestableHwnd = new WindowInteropHelper(m_nonHitTestableWindow).EnsureHandle();

            _ = PInvoke.SetWindowLong(new(m_hwnd), GetWindowLongPtr_nIndex.GWL_EXSTYLE,
                (int)(WINDOWS_EX_STYLE.WS_EX_TOOLWINDOW | WINDOWS_EX_STYLE.WS_EX_NOACTIVATE));
            _ = PInvoke.SetWindowLong(new(m_nonHitTestableHwnd), GetWindowLongPtr_nIndex.GWL_EXSTYLE,
                (int)(WINDOWS_EX_STYLE.WS_EX_TOOLWINDOW| WINDOWS_EX_STYLE.WS_EX_TRANSPARENT | WINDOWS_EX_STYLE.WS_EX_NOACTIVATE));

            DisableWindow(m_hwnd);
            DisableWindow(m_nonHitTestableHwnd);
            UpdatePositions();

            display.Workspace.CursorLocationChanged += OnCursorLocationChanged;
        }

        public void Show()
        {
            m_nonHitTestableWindow.Show();
            // Hit-testable window goes on top, because it after an interaction,
            // it will be raised above the non-hit testable window anyway.
            m_window.Show();
            m_isShown = true;

            async void UpdateLoop()
            {
                while (m_isShown)
                {
                    UpdatePositions();
                    await Task.Delay(250);
                }
            }
            UpdateLoop();
        }

        internal void Hide()
        {
            m_isShown = false;
            m_nonHitTestableWindow.Hide();
            m_window.Hide();
        }

        public void Close()
        {
            m_isShown = false;
            m_isClosed = true;
            AnchorSource = null;
            m_display.Workspace.CursorLocationChanged -= OnCursorLocationChanged;
            Dispatcher.Invoke(() =>
            {
                m_nonHitTestableWindow.Visibility = Visibility.Collapsed;
                m_nonHitTestableWindow.AllowClose = true;
                m_nonHitTestableWindow.Close();
                m_window.Visibility = Visibility.Collapsed;
                m_window.AllowClose = true;
                m_window.Close();
            });
        }

        public void UpdatePositions()
        {
            var hwnd = new WindowInteropHelper(m_window).EnsureHandle();
            var nonHitTestableHwnd = new WindowInteropHelper(m_nonHitTestableWindow).EnsureHandle();

            var anchor = AnchorSource != null
                ? AnchorSource()
                : new IntPtr(-1);

            var beforeAnchor = PInvoke.GetWindow(new(anchor), GetWindow_uCmdFlags.GW_HWNDPREV);
            if (beforeAnchor.Value == default)
            {
                if (PInvoke.IsWindow(new(anchor)))
                {
                    PInvoke.SetWindowPos(new(hwnd), new(anchor), 0, 0, 0, 0,
                        SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                    PInvoke.SetWindowPos(new(nonHitTestableHwnd), new(anchor), 0, 0, 0, 0,
                        SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                    PInvoke.SetWindowPos(new(nonHitTestableHwnd), new(hwnd), 0, 0, 0, 0,
                        SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                    PInvoke.SetWindowPos(new(anchor), new(nonHitTestableHwnd), 0, 0, 0, 0,
                        SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                }
                else
                {
                    PInvoke.SetWindowPos(new(hwnd), new(), 0, 0, 0, 0,
                        SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                    PInvoke.SetWindowPos(new(nonHitTestableHwnd), new(), 0, 0, 0, 0,
                        SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                    PInvoke.SetWindowPos(new(nonHitTestableHwnd), new(), 0, 0, 0, 0,
                        SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                }
            }
            else
            {
                PInvoke.SetWindowPos(new(hwnd), new(beforeAnchor), 0, 0, 0, 0,
                    SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
                PInvoke.SetWindowPos(new(nonHitTestableHwnd), new(hwnd), 0, 0, 0, 0,
                    SetWindowPos_uFlags.SWP_NOMOVE | SetWindowPos_uFlags.SWP_NOSIZE | SetWindowPos_uFlags.SWP_NOACTIVATE | SetWindowPos_uFlags.SWP_NOSENDCHANGING);
            }
        }

        public void SetResource(string key, object? value)
        {
            m_window.Resources[key] = value;
            m_nonHitTestableWindow.Resources[key] = value;
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == ContentProperty)
            {
                m_window.SetContent(Content!);
            }
            else if (e.Property == NonHitTestableContentProperty)
            {
                m_nonHitTestableWindow.SetContent(NonHitTestableContent!);
            }
        }

        private void EnableWindow(IntPtr hwnd)
        {
            var oldValue = PInvoke.GetWindowLong(new(hwnd), GetWindowLongPtr_nIndex.GWL_STYLE);
            var newValue = oldValue & ~(int)WINDOWS_STYLE.WS_DISABLED;
            if (oldValue != newValue)
            {
                _ = PInvoke.SetWindowLong(new(hwnd), GetWindowLongPtr_nIndex.GWL_STYLE, newValue);
            }
        }

        private void DisableWindow(IntPtr hwnd)
        {
            var oldValue = PInvoke.GetWindowLong(new(hwnd), GetWindowLongPtr_nIndex.GWL_STYLE);
            var newValue = oldValue | (int)WINDOWS_STYLE.WS_DISABLED;
            if (oldValue != newValue)
            {
                _ = PInvoke.SetWindowLong(new(hwnd), GetWindowLongPtr_nIndex.GWL_STYLE, newValue);
            }
        }

        private volatile bool m_isScheduled = false;
        private void OnCursorLocationChanged(object? sender, CursorLocationChangedEventArgs e)
        {
            if (!m_isScheduled)
            {
                m_isScheduled = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    m_isScheduled = false;
                    var screenPoint = m_display.Workspace.CursorLocation;
                    bool wasHit;
                    try
                    {
                        var point = m_window.PointFromScreen(new(screenPoint.X, screenPoint.Y));
                        wasHit = VisualTreeHelper.HitTest(m_window, point) != null;
                    }
                    catch (InvalidOperationException)
                    {
                        // This Visual is not connected to a PresentationSource.
                        wasHit = false;
                    }

                    if (wasHit)
                    {
                        EnableWindow(m_hwnd);
                    }
                    else
                    {
                        DisableWindow(m_hwnd);
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
    }
}
