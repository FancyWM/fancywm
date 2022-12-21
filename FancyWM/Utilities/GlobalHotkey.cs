using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using FancyWM.DllImports;

namespace FancyWM.Utilities
{
    /// <summary>
    /// Allows the user to register a global hotkey.
    /// </summary>
    internal sealed class GlobalHotkey : IDisposable
    {
        public event Action? Pressed;

        private static int s_id = 1;

        private RegisterHotKey_fsModifiersFlags _modifier;
        private KeyCode _key;
        private IntPtr _hWnd;
        private HwndSource _hWndSource;
        private HwndSourceHook _hWndSourceHook;
        private int _id;
        private bool _isRegistered;
        private bool _isDisposed;

        public GlobalHotkey(IntPtr hwnd, RegisterHotKey_fsModifiersFlags modifier, KeyCode key)
        {
            _modifier = modifier;
            _key = key;
            _id = Interlocked.Increment(ref s_id);
            _isRegistered = false;
            _hWnd = hwnd;
            _hWndSource = HwndSource.FromHwnd(_hWnd);
            _hWndSourceHook = WndProc;
            _hWndSource.AddHook(_hWndSourceHook);
            _isDisposed = false;
        }

        public GlobalHotkey(IntPtr hwnd, IReadOnlyList<KeyCode> modifiers, KeyCode key) : this(hwnd, ConvertToModifierFlags(modifiers), key)
        {
        }

        private static RegisterHotKey_fsModifiersFlags ConvertToModifierFlags(IReadOnlyList<KeyCode> modifiers)
        {
            var modifierSet = modifiers.ToHashSet();
            RegisterHotKey_fsModifiersFlags flags = default;
            if (modifierSet.Remove(KeyCode.LControlKey) || modifierSet.Remove(KeyCode.RControlKey))
            {
                flags |= RegisterHotKey_fsModifiersFlags.MOD_CONTROL;
            }
            if (modifierSet.Remove(KeyCode.LShiftKey) || modifierSet.Remove(KeyCode.RShiftKey))
            {
                flags |= RegisterHotKey_fsModifiersFlags.MOD_SHIFT;
            }
            if (modifierSet.Remove(KeyCode.LMenu) || modifierSet.Remove(KeyCode.RMenu))
            {
                flags |= RegisterHotKey_fsModifiersFlags.MOD_ALT;
            }
            if (modifierSet.Remove(KeyCode.LWin) || modifierSet.Remove(KeyCode.RWin))
            {
                flags |= RegisterHotKey_fsModifiersFlags.MOD_WIN;
            }

            if (modifierSet.Count > 0)
            {
                throw new ArgumentException($"Some keys are not modifier keys: {string.Join(", ", modifiers)}", nameof(modifiers));
            }

            return flags;
        }

        public override int GetHashCode()
        {
            return (int)_modifier ^ (int)_key ^ _hWnd.ToInt32();
        }

        /// <summary>
        /// Registers the hotkey.
        /// </summary>
        public void Register()
        {
            if (_isRegistered)
            {
                return;
            }
            if (!PInvoke.RegisterHotKey(new(_hWnd), _id, _modifier, (uint)_key))
            {
                throw new Win32Exception("Couldn't register hotkey.");
            }
            _isRegistered = true;
        }

        /// <summary>
        /// Unregisters the hotkey.
        /// </summary>
        public void Unregiser()
        {
            if (!_isRegistered)
            {
                return;
            }
            if (!PInvoke.UnregisterHotKey(new(_hWnd), _id))
            {
                throw new Win32Exception("Couldn't unregister hotkey.");
            }
        }

        private const int WM_HOTKEY = 0x0312;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && _id == wParam.ToInt32())
            {
                Pressed?.Invoke();
                handled = true;
                return IntPtr.Zero;
            }
            handled = false;
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            Pressed = null;
            Unregiser();
            _hWndSource.RemoveHook(WndProc);
            _isDisposed = true;
        }
    }
}
