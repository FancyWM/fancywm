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
        public required bool ScanOnRelease { get; init; }
        public required bool HideKeyPress { get; init; }
        public required bool ClearModifiersOnMiss { get; init; }
        public required bool SideAgnostic { get; set; }

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

        private KeyCode RemapKeyCode(KeyCode k)
        {
            if (!SideAgnostic)
            {
                return k;
            }
            return k switch
            {
                KeyCode.RightShift => KeyCode.LeftShift,
                KeyCode.RightCtrl => KeyCode.LeftCtrl,
                KeyCode.RWin => KeyCode.LWin,
                KeyCode.RightAlt => KeyCode.LeftAlt,
                _ => k,
            };
        }

        private void OnLowLevelKeyStateChanged(object? sender, ref LowLevelKeyboardHook.KeyStateChangedEventArgs e)
        {
            var inputKeyCode = RemapKeyCode(e.KeyCode);
            var mainKeyCode = RemapKeyCode(Key);

            bool Scan(ref LowLevelKeyboardHook.KeyStateChangedEventArgs e)
            {
                if (m_pressedModifiers.All(x => x) && inputKeyCode == mainKeyCode)
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
                else if (inputKeyCode != mainKeyCode && ClearModifiersOnMiss)
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

                // Handle the dirty key.
                if (inputKeyCode == mainKeyCode && m_keyDirty)
                {
                    m_keyDirty = false;
                    e.Handled = true;
                }

                // 1. If we want to trigger on release.
                // 2. Check if the hotkey is triggered (requires main key).
                if (ScanOnRelease)
                {
                    Scan(ref e);
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
