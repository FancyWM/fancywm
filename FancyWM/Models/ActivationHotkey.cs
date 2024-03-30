using System;
using System.Collections.Generic;
using System.Linq;

using FancyWM.Utilities;

namespace FancyWM.Models
{
    public class ActivationHotkey
    {
        public KeyCode[] ModifierKeys { get; }
        public KeyCode Key { get; }
        public string Description { get; }

        public static IReadOnlyList<ActivationHotkey> AllowedHotkeys { get; } =
        [
            new([KeyCode.LeftShift], KeyCode.LWin, "⇧ + ⊞"),
            new([KeyCode.LeftCtrl], KeyCode.LWin, "Ctrl + ⊞"),
            new([KeyCode.LeftAlt], KeyCode.LWin, "Alt + ⊞"),
            new([KeyCode.None], KeyCode.None, "Disabled"),
        ];

        public static ActivationHotkey Default => AllowedHotkeys[0];

        private ActivationHotkey(KeyCode[] modifierKeys, KeyCode key, string description)
        {
            ModifierKeys = [..modifierKeys.OrderBy(x => (int)x)];
            Key = key;
            Description = description;
        }

        public override bool Equals(object? obj)
        {
            return obj is ActivationHotkey hotkey &&
                   ModifierKeys.Equals(hotkey.ModifierKeys) &&
                   Key == hotkey.Key;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ModifierKeys, Key);
        }

        public override string ToString()
        {
            return Description;
        }
    }
}
