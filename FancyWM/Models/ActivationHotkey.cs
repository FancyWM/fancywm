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
            new([KeyCode.LeftAlt], KeyCode.LWin, "Alt + Win ⊞"),
            new([KeyCode.LeftAlt], KeyCode.LeftCtrl, "Alt + Ctrl"),
            new([KeyCode.LeftAlt], KeyCode.LeftShift, "Alt + Shift ⇧"),
            new([KeyCode.LeftCtrl], KeyCode.LWin, "Ctrl + Win ⊞"),
            new([KeyCode.LeftCtrl], KeyCode.LeftShift, "Ctrl + Shift ⇧"),
            new([KeyCode.LeftShift], KeyCode.LWin, "Shift ⇧ + Win ⊞"),
            new([KeyCode.LeftAlt, KeyCode.LeftCtrl], KeyCode.LWin, "Alt + Ctrl + Win ⊞"),
            new([KeyCode.LeftAlt, KeyCode.LeftCtrl], KeyCode.LeftShift, "Alt + Ctrl + Shift ⇧"),
            new([KeyCode.LeftAlt, KeyCode.LeftShift], KeyCode.LWin, "Alt + Shift ⇧ + Win ⊞"),
            new([KeyCode.LeftCtrl, KeyCode.LeftShift], KeyCode.LWin, "Ctrl + Shift ⇧ + Win ⊞"),
            new([KeyCode.None], KeyCode.None, "Disabled"),
        ];

        public static ActivationHotkey Default { get; } = AllowedHotkeys.First(x => x.Description == "Shift ⇧ + Win ⊞");

        private ActivationHotkey(KeyCode[] modifierKeys, KeyCode key, string description)
        {
            ModifierKeys = [.. modifierKeys.OrderBy(x => (int)x)];
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
