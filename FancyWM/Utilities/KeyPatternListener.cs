using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

using Microsoft.Extensions.DependencyInjection;

namespace FancyWM.Utilities
{
    internal class KeyPatternChangedEventArgs(IReadOnlySet<KeyCode> keys) : EventArgs
    {
        public IReadOnlySet<KeyCode> Keys { get; } = keys ?? throw new ArgumentNullException(nameof(keys));
    }

    internal delegate void KeyPatternChangedEventHandler(object sender, KeyPatternChangedEventArgs e);

    internal interface IKeyPatternListener : IDisposable
    {
        event KeyPatternChangedEventHandler? PatternChanged;

        bool IsListening { get; }

        IReadOnlySet<KeyCode>? Pattern { get; }
    }

    internal class KeyPatternListener : IKeyPatternListener
    {
        public event KeyPatternChangedEventHandler? PatternChanged;

        public bool IsListening { get; private set; }

        public IReadOnlySet<KeyCode>? Pattern { get; private set; }

        public UIElement EventSource { get; }

        private readonly LowLevelKeyboardHook m_llKbdHk = App.Current.Services.GetRequiredService<LowLevelKeyboardHook>();

        private readonly HashSet<KeyCode> m_pressedKeys = [];

        public KeyPatternListener(UIElement eventSource)
        {
            EventSource = eventSource;
            EventSource.IsKeyboardFocusWithinChanged += OnKeyboardFocusChanged;
        }

        private void OnKeyboardFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            m_pressedKeys.Clear();

            if (EventSource.IsKeyboardFocusWithin)
            {
                IsListening = true;
                m_llKbdHk.KeyStateChanged += OnKeyStateChanged;
            }
            else
            {
                IsListening = false;
                m_llKbdHk.KeyStateChanged -= OnKeyStateChanged;
            }
        }

        public void Dispose()
        {
            m_llKbdHk.KeyStateChanged -= OnKeyStateChanged;
            EventSource.IsKeyboardFocusWithinChanged -= OnKeyboardFocusChanged;
            IsListening = false;
        }

        private void OnKeyStateChanged(object? sender, ref LowLevelKeyboardHook.KeyStateChangedEventArgs e)
        {
            e.Handled = true;
            var eCopy = e;
            _ = EventSource.Dispatcher.InvokeAsync(() =>
            {
                if (eCopy.IsPressed)
                {
                    m_pressedKeys.Add(eCopy.KeyCode);
                }
                else
                {
                    if (m_pressedKeys.Count > 0)
                    {
                        Pattern = m_pressedKeys.ToHashSet();
                        m_pressedKeys.Clear();
                        PatternChanged?.Invoke(this, new KeyPatternChangedEventArgs(Pattern));
                    }
                }
            });
        }
    }

    public static class KeySetExtensions
    {
        public static KeyCode Normalize(this KeyCode key)
        {
            return key switch
            {
                KeyCode.RightCtrl => KeyCode.LeftCtrl,
                KeyCode.RightAlt => KeyCode.LeftAlt,
                KeyCode.RightShift => KeyCode.LeftShift,
                KeyCode.RWin => KeyCode.LWin,
                _ => key,
            };
        }

        public static IEnumerable<KeyCode> Normalize(this IEnumerable<KeyCode> keys)
        {
            return keys.Select(x => x.Normalize());
        }

        public static bool SetEqualsSideInsensitive(this IReadOnlySet<KeyCode> keys, IEnumerable<KeyCode> enumerable)
        {
            return keys.Select(x => x.Normalize()).ToHashSet().SetEquals(enumerable.Normalize());
        }

        public static string ToPrettyString(this IEnumerable<KeyCode> keys)
        {
            if (!keys.Any())
            {
                throw new ArgumentException("Empty key set!");
            }
            return string.Join(" + ", keys.Select(key => KeyDescriptions.GetDescription(key)));
        }
    }
}
