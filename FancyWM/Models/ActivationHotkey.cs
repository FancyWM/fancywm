using System;
using System.Collections.Generic;

using FancyWM.Utilities;

namespace FancyWM.Models
{
    public class ActivationHotkey
    {
        public KeyCode KeyA { get; }
        public KeyCode KeyB { get; }
        public string Description { get; }

        public static IReadOnlyList<ActivationHotkey> AllowedHotkeys { get; } = new ActivationHotkey[]
        {
            new(KeyCode.LeftShift, KeyCode.LWin, "⇧ + ⊞"),
            new(KeyCode.LeftCtrl, KeyCode.LWin, "Ctrl + ⊞"),
            new(KeyCode.LeftAlt, KeyCode.LWin, "Alt + ⊞"),
            new(KeyCode.None, KeyCode.None, "Disabled"),
        };

        public static ActivationHotkey Default => AllowedHotkeys[0];

        private ActivationHotkey(KeyCode keyA, KeyCode keyB, string description)
        {
            if ((int)keyA <= (int)keyB)
            {
                KeyA = keyA;
                KeyB = keyB;
            }
            else
            {
                KeyA = keyB;
                KeyB = keyA;
            }
            Description = description;
        }

        public override bool Equals(object? obj)
        {
            return obj is ActivationHotkey hotkey &&
                   KeyA == hotkey.KeyA &&
                   KeyB == hotkey.KeyB;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(KeyA, KeyB);
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
