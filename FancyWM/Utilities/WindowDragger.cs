using System;

using WinMan;
using WinMan.Windows;

namespace FancyWM.Utilities
{
    internal class WindowDragger
    {
        public IWindow Window => m_window;

        private Win32Window m_window;
        private Rectangle m_originalRect;
        private int m_xOffset;
        private int m_yOffset;

        public WindowDragger(IWindow window)
        {
            m_window = (Win32Window)window;
            m_originalRect = window.Position;
            m_xOffset = m_window.Workspace.CursorLocation.X - m_originalRect.Left;
            m_yOffset = m_window.Workspace.CursorLocation.Y - m_originalRect.Top;
        }

        internal void Begin(bool activateWindow)
        {
            if (m_window.State != WindowState.Restored)
            {
                m_window.SetState(WindowState.Restored);
            }
            if (activateWindow)
            {
                FocusHelper.ForceActivate(m_window.Handle);
            }
            m_window.RaisePositionChangeStart();
            m_window.Workspace.CursorLocationChanged += OnCursorLocationChanged;
        }

        internal void End()
        {
            m_window.Workspace.CursorLocationChanged -= OnCursorLocationChanged;
            m_window.RaisePositionChangeEnd();
        }

        private void OnCursorLocationChanged(object? sender, CursorLocationChangedEventArgs e)
        {
            try
            {
                m_window.SetPosition(Rectangle.OffsetAndSize(e.NewLocation.X - m_xOffset, e.NewLocation.Y - m_yOffset, m_originalRect.Width, m_originalRect.Height));
            }
            catch (InvalidWindowReferenceException)
            {
                m_window.Workspace.CursorLocationChanged -= OnCursorLocationChanged;
            }
            catch (Exception)
            {
                m_window.Workspace.CursorLocationChanged -= OnCursorLocationChanged;
            }
        }
    }
}