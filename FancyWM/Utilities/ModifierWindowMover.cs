using System;
using System.Windows.Input;

using Serilog;

using WinMan;

namespace FancyWM.Utilities
{
    internal class ModifierWindowMover : IDisposable
    {
        public bool IsEnabled { get; set; }

        public bool AutoFocus { get; set; }

        private readonly ILogger m_logger = App.Current.Logger;
        private readonly LowLevelMouseHook m_mshk;
        private readonly IWorkspace m_workspace;
        private WindowDragger? m_windowDragger = null;

        public ModifierWindowMover(IWorkspace workspace, LowLevelMouseHook mshk)
        {
            m_workspace = workspace;
            m_mshk = mshk;
            m_mshk.ButtonStateChanged += OnMouseButtonStateChanged;
        }

        private void OnMouseButtonStateChanged(object? sender, ref LowLevelMouseHook.ButtonStateChangedEventArgs e)
        {
            e.Handled = false;
            if (!IsEnabled || e.Button != LowLevelMouseHook.MouseButton.Left)
            {
                return;
            }

            if (m_windowDragger is WindowDragger windowDragger)
            {
                e.Handled = true;
                m_windowDragger = null;
                windowDragger.End();
                return;
            }
            else
            {
                if (!e.IsPressed || !IsMoveModifierPressed())
                {
                    return;
                }
            }

            try
            {
                var window = m_workspace.FindWindowFromPoint(new(e.X, e.Y));
                if (window == null)
                {
                    return;
                }

                m_windowDragger = new WindowDragger(window);
                bool activateWindow = AutoFocus || IsMoveActivateModifierPressed();
                try
                {
                    m_windowDragger.Begin(activateWindow);
                }
                catch (Exception)
                {
                    m_windowDragger.End();
                    m_windowDragger = null;
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                m_logger.Error(ex, "Ocurred during drag mouse hook!");
            }
        }

        private static bool IsMoveModifierPressed()
        {
            static bool GetState() => Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
            if (App.Current.Dispatcher.CheckAccess())
            {
                return GetState();
            }
            else
            {
                return App.Current.Dispatcher.Invoke(GetState, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        private static bool IsMoveActivateModifierPressed()
        {
            static bool GetState() => Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            if (App.Current.Dispatcher.CheckAccess())
            {
                return GetState();
            }
            else
            {
                return App.Current.Dispatcher.Invoke(GetState, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        public void Dispose()
        {
            m_mshk.ButtonStateChanged -= OnMouseButtonStateChanged;
        }
    }
}
