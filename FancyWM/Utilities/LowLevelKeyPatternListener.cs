using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace FancyWM.Utilities
{
    internal class LowLevelKeyPatternListener : IKeyPatternListener
    {
        public Dispatcher Dispatcher { get; }

        public LowLevelKeyboardHook KeyboardHook { get; }

        public bool IsListening { get; private set; }

        public IReadOnlySet<KeyCode>? Pattern { get; private set; }

        private readonly HashSet<KeyCode> m_pressedKeys = new HashSet<KeyCode>();
        private readonly HashSet<KeyCode> m_pressedKeyCodes = new HashSet<KeyCode>();

        public event KeyPatternChangedEventHandler? PatternChanged;

        public LowLevelKeyPatternListener(LowLevelKeyboardHook keyboardHook)
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            KeyboardHook = keyboardHook;
            KeyboardHook.KeyStateChanged += OnKeyStateChanged;
            IsListening = true;
        }

        public void Dispose()
        {
            if (!IsListening)
                return;
            if (m_pressedKeyCodes.Count > 0)
            {
                KeyboardHook.KeyStateChanged += CleanupKeyState;
            }
            KeyboardHook.KeyStateChanged -= OnKeyStateChanged;
            IsListening = false;
        }

        private void CleanupKeyState(object? sender, ref LowLevelKeyboardHook.KeyStateChangedEventArgs e)
        {
            if (m_pressedKeyCodes.Remove(e.KeyCode))
            {
                e.Handled = true;
            }

            if (m_pressedKeyCodes.Count == 0)
            {
                KeyboardHook.KeyStateChanged -= CleanupKeyState;
            }
        }

        private void OnKeyStateChanged(object? sender, ref LowLevelKeyboardHook.KeyStateChangedEventArgs e)
        {
            if (e.IsPressed)
            {
                e.Handled = true;
                m_pressedKeyCodes.Add(e.KeyCode);
                m_pressedKeys.Add(e.KeyCode);
            }
            else
            {
                bool wasPressed = m_pressedKeyCodes.Remove(e.KeyCode);
                if (wasPressed)
                {
                    e.Handled = true;
                }

                if (m_pressedKeys.Count > 0)
                {
                    Pattern = m_pressedKeys.ToHashSet();
                    m_pressedKeys.Clear();
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PatternChanged?.Invoke(this, new KeyPatternChangedEventArgs(Pattern));
                    }));
                }
            }
        }
    }
}
