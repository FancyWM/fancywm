using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FancyWM.Utilities
{
    internal class KeyPatternChangedEventArgs : EventArgs
    {
        public IReadOnlySet<Key> Keys { get; }

        public KeyPatternChangedEventArgs(IReadOnlySet<Key> keys)
        {
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));
        }
    }

    internal delegate void KeyPatternChangedEventHandler(object sender, KeyPatternChangedEventArgs e);

    internal interface IKeyPatternListener : IDisposable
    {
        event KeyPatternChangedEventHandler? PatternChanged;

        bool IsListening { get; }

        IReadOnlySet<Key>? Pattern { get; }
    }

    internal class KeyPatternListener : IKeyPatternListener
    {
        public event KeyPatternChangedEventHandler? PatternChanged;

        public bool IsListening { get; private set; }

        public IReadOnlySet<Key>? Pattern { get; private set; }

        public UIElement EventSource { get; }

        private readonly HashSet<Key> m_pressedKeys = new HashSet<Key>();

        public KeyPatternListener(UIElement eventSource)
        {
            EventSource = eventSource;
            EventSource.PreviewKeyUp += OnKeyUp;
            EventSource.PreviewKeyDown += OnKeyDown;
            EventSource.LostKeyboardFocus += OnLostKeyboardFocus;
            EventSource.Focus();
            IsListening = true;
        }

        public void Dispose()
        {
            if (!IsListening)
                return;
            EventSource.PreviewKeyUp -= OnKeyUp;
            EventSource.PreviewKeyDown -= OnKeyDown;
            EventSource.LostKeyboardFocus -= OnLostKeyboardFocus;
            IsListening = false;
        }

        private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            m_pressedKeys.Clear();
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (m_pressedKeys.Count > 0)
            {
                Pattern = m_pressedKeys.ToHashSet();
                m_pressedKeys.Clear();
                PatternChanged?.Invoke(this, new KeyPatternChangedEventArgs(Pattern));
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.SystemKey != Key.None)
            {
                m_pressedKeys.Add(e.SystemKey);
            }
            else
            {
                m_pressedKeys.Add(e.Key);
            }
        }
    }

    public static class KeySetExtensions
    {
        public static Key Normalize(this Key key)
        {
            return key switch
            {
                Key.RightCtrl => Key.LeftCtrl,
                Key.RightAlt => Key.LeftAlt,
                Key.RightShift => Key.LeftShift,
                Key.RWin => Key.LWin,
                _ => key,
            };
        }

        public static IEnumerable<Key> Normalize(this IEnumerable<Key> keys)
        {
            return keys.Select(x => x.Normalize());
        }

        public static bool SetEqualsSideInsensitive(this IReadOnlySet<Key> keys, IEnumerable<Key> enumerable)
        {
            return keys.Select(x => x.Normalize()).ToHashSet().SetEquals(enumerable.Normalize());
        }

        public static string ToPrettyString(this IEnumerable<Key> keys)
        {
            if (!keys.Any())
            {
                throw new ArgumentException("Empty key set!");
            }
            return string.Join(" + ", keys.Select(key => KeyDescriptions.GetDescription(key)));
        }
    }
}
