using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

using FancyWM.DllImports;

using WinMan;

namespace FancyWM.Windows
{
    partial class OverlayHost
    {
        private class OverlayWindow : Window
        {
            private readonly IDisplay m_display;
            private readonly Border m_contentContainer;
            private readonly IDisposable m_subscription;
            private readonly IntPtr m_hwnd;
            private int m_panelFontSize;

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
                SnapsToDevicePixels = true;

                m_hwnd = new WindowInteropHelper(this).EnsureHandle();
                m_contentContainer = new Border();
                Content = m_contentContainer;
                UpdateRenderTransform();

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

            private Rectangle? GetWindowRectangle()
            {
                if (PInvoke.GetWindowRect(new(m_hwnd), out RECT current))
                {
                    return new Rectangle(current.left, current.top, current.right, current.bottom);
                }
                return default;
            }

            private void EnsureLocation()
            {
                var current = GetWindowRectangle();
                var desired = m_display.WorkArea;
                if (current != desired)
                {
                    PInvoke.SetWindowPos(new(m_hwnd), new(), desired.Left, desired.Top, desired.Width, desired.Height,
                        SetWindowPos_uFlags.SWP_NOZORDER | SetWindowPos_uFlags.SWP_NOACTIVATE);
                }
            }

            private void UpdateRenderTransform()
            {
                EnsureLocation();
                var bounds = m_display.Bounds;
                var workArea = m_display.WorkArea;

                var tg = new TransformGroup();
                tg.Children.Add(new TranslateTransform(
                    bounds.Left - workArea.Left,
                    bounds.Top - workArea.Top));
                tg.Children.Add(new ScaleTransform(
                        1 / m_display.Scaling,
                        1 / m_display.Scaling));
                m_contentContainer.RenderTransform = tg;
            }

            private void OnDisplayScalingChanged(object? sender, DisplayScalingChangedEventArgs e)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateRenderTransform();
                    Resources["DisplayScaling"] = m_display.Scaling;
                    Resources["OverlayFontSize"] = m_panelFontSize * m_display.Scaling;
                });
            }

            private void OnDisplayWorkAreaChanged(object? sender, DisplayRectangleChangedEventArgs e)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateRenderTransform();
                });
            }

            protected override void OnLocationChanged(EventArgs e)
            {
                base.OnLocationChanged(e);
                EnsureLocation();
            }

            protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
            {
                base.OnRenderSizeChanged(sizeInfo);
                EnsureLocation();
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
    }
}
