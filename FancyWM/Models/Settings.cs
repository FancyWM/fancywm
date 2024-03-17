using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;

using FancyWM.Utilities;

namespace FancyWM.Models
{
    public interface ITilingServiceSettings
    {
        bool AllocateNewPanelSpace { get; }
        bool AnimateWindowMovement { get; }
        int WindowPadding { get; }
        int PanelHeight { get; }
    }

    public class DefaultKeybindingAttribute : Attribute
    {
        public readonly KeyCode[] Keys;

        public DefaultKeybindingAttribute(params KeyCode[] keys)
        {
            Keys = keys;
        }
    }

    public class Settings : IEquatable<Settings>, ICloneable, ITilingServiceSettings
    {

        public Settings()
        {

        }

        [JsonConverter(typeof(Converters.ActivationHotkeyConverter))]
        public ActivationHotkey ActivationHotkey { get; set; } = ActivationHotkey.Default;

        public bool ShowStartupWindow { get; set; } = true;

        public bool AllocateNewPanelSpace { get; set; } = true;

        public bool AutoCollapsePanels { get; set; } = true;

        public bool AnimateWindowMovement { get; set; } = true;

        public bool ModifierMoveWindow { get; set; } = false;

        public bool ModifierMoveWindowAutoFocus { get; set; } = false;

        public int WindowPadding { get; set; } = 4;

        public int PanelHeight { get; set; } = 18;

        public int PanelFontSize { get; set; } = 12;

        public bool ShowFocusDuringAction { get; set; } = true;

        public bool OverrideAccentColor { get; set; } = false;

        [JsonConverter(typeof(Converters.ColorConverter))]
        public Color CustomAccentColor { get; set; } = Colors.Blue;

        [JsonConverter(typeof(Converters.KeybindingConverter))]
        public KeybindingDictionary Keybindings { get; set; } = new KeybindingDictionary();

        public List<string> ProcessIgnoreList { get; set; } = new List<string>
        {
            "Taskmgr"
        };

        public List<string> ClassIgnoreList { get; set; } = new List<string>
        {
            "OperationStatusWindow"
        };

        public bool RemindToRateReview { get; set; } = true;

        public bool ShowContextHints { get; set; } = true;

        public bool MultiMonitorSupport { get; set; } = true;

        public bool SoundOnFailure { get; set; } = true;

        public override bool Equals(object? obj)
        {
            return obj is Settings settings &&
                   ShowStartupWindow == settings.ShowStartupWindow &&
                   AllocateNewPanelSpace == settings.AllocateNewPanelSpace &&
                   RemindToRateReview == settings.RemindToRateReview &&
                   ShowContextHints == settings.ShowContextHints &&
                   ModifierMoveWindow == settings.ModifierMoveWindow &&
                   AnimateWindowMovement == settings.AnimateWindowMovement &&
                   OverrideAccentColor == settings.OverrideAccentColor &&
                   CustomAccentColor.Equals(settings.CustomAccentColor) &&
                   Equals(Keybindings, settings.Keybindings) &&
                   Equals(ProcessIgnoreList, settings.ProcessIgnoreList) &&
                   Equals(ClassIgnoreList, settings.ClassIgnoreList) &&
                   WindowPadding == settings.WindowPadding &&
                   PanelHeight == settings.PanelHeight &&
                   PanelFontSize == settings.PanelFontSize &&
                   ShowFocusDuringAction == settings.ShowFocusDuringAction;
        }

        private bool Equals(IDictionary<BindableAction, IReadOnlySet<KeyCode>?> left, IDictionary<BindableAction, IReadOnlySet<KeyCode>?> right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (!left.Keys.SequenceEqual(right.Keys))
                return false;

            foreach (var key in left.Keys)
            {
                if (left[key] == null)
                    if (right[key] != null)
                        return false;
                if (right[key] == null)
                    return false;
                if (!left[key]!.SetEquals(right[key]!))
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(ShowStartupWindow);
            hash.Add(AllocateNewPanelSpace);
            hash.Add(RemindToRateReview);
            hash.Add(ShowContextHints);
            hash.Add(ModifierMoveWindow);
            hash.Add(AnimateWindowMovement);
            hash.Add(OverrideAccentColor);
            hash.Add(CustomAccentColor);
            hash.Add(Keybindings);
            hash.Add(WindowPadding);
            hash.Add(PanelHeight);
            hash.Add(PanelFontSize);
            hash.Add(ShowFocusDuringAction);
            hash.Add(ProcessIgnoreList);
            hash.Add(ClassIgnoreList);
            return hash.ToHashCode();
        }

        public bool Equals(Settings? other)
        {
            return Equals((object?)other);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public Settings Clone()
        {
            return (Settings)MemberwiseClone();
        }
    }
}
