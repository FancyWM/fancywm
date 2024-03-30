using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace FancyWM.Utilities
{
    internal class LowLevelHotkey : IDisposable
    {
        public event EventHandler<EventArgs>? Pressed;

        public Dispatcher Dispatcher { get; }
        public LowLevelKeyboardHook KeyboardHook { get; }
        public IReadOnlyCollection<KeyCode> ModifierKeys => m_modifiers;
        public KeyCode Key { get; }
        public bool ScanOnRelease { get; init; } = false;
        public bool HideKeyPress { get; init; } = true;
        public bool ClearModifiersOnMiss { get; init; } = false;

        private readonly KeyCode[] m_modifiers;
        private readonly bool[] m_pressedModifiers;
        private bool m_keyDirty = false;

        public LowLevelHotkey(LowLevelKeyboardHook keyboardHook, IReadOnlyCollection<KeyCode> modifierKeys, KeyCode key)
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            KeyboardHook = keyboardHook ?? throw new ArgumentNullException(nameof(keyboardHook));
            Key = key;

            m_modifiers = modifierKeys.ToArray() ?? throw new ArgumentNullException(nameof(modifierKeys)); ;
            m_pressedModifiers = new bool[modifierKeys.Count];
            KeyboardHook.KeyStateChanged += OnLowLevelKeyStateChanged;
        }

        private void OnLowLevelKeyStateChanged(object? sender, ref LowLevelKeyboardHook.KeyStateChangedEventArgs e)
        {
            bool Scan(ref LowLevelKeyboardHook.KeyStateChangedEventArgs e)
            {
                if (m_pressedModifiers.All(x => x) && e.KeyCode == Key)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        Pressed?.Invoke(this, new EventArgs());
                    });
                    return true;
                }
                return false;
            }
            
            if (e.IsPressed)
            {
                // 1. Unless we want the trigger on release.
                // 2. Check if the hotkey is triggered (requires main key).
                // 3. And if so, if we need to hide the main key, do so.
                if (!ScanOnRelease && Scan(ref e) && HideKeyPress)
                {
                    m_keyDirty = true;
                    e.Handled = true;
                }

                int modifierIndex = Array.IndexOf(m_modifiers, e.KeyCode);
                if (modifierIndex != -1)
                {
                    m_pressedModifiers[modifierIndex] = true;
                }
                else if (e.KeyCode != Key && ClearModifiersOnMiss)
                {
                    // A non-modifier, non-main key was pressed, in which case
                    // we reset the state, to allow other hotkeys to trigger.
                    Array.Fill(m_pressedModifiers, false);
                }
            }
            else
            {
                int modifierIndex = Array.IndexOf(m_modifiers, e.KeyCode);
                if (modifierIndex != -1)
                {
                    m_pressedModifiers[modifierIndex] = false;
                }

                // 1. If we want to trigger on release.
                // 2. Check if the hotkey is triggered (requires main key).
                // 3. And if so, if we need to hide the main key, do so.
                if (ScanOnRelease && Scan(ref e) && HideKeyPress)
                {
                    if (e.KeyCode == Key && m_keyDirty)
                    {
                        e.Handled = true;
                    }
                }
            }
        }

        public void Dispose()
        {
            Pressed = null;
            KeyboardHook.KeyStateChanged -= OnLowLevelKeyStateChanged;
        }
    }
}
