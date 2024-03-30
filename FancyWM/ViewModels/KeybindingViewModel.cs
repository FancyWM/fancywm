using System.Collections.Generic;
using System.Linq;

using FancyWM.Models;
using FancyWM.Utilities;

namespace FancyWM.ViewModels
{
    public class KeybindingViewModel : ViewModelBase
    {
        private BindableAction m_action;
        private IReadOnlySet<KeyCode>? m_pattern;
        private bool m_hasErrors;
        private bool m_isDirectMode;

        public BindableAction Action
        {
            get => m_action;
            set => SetField(ref m_action, value);
        }

        public IReadOnlySet<KeyCode>? Pattern
        {
            get => m_pattern;
            set => SetField(ref m_pattern, value);
        }

        [DerivedProperty(nameof(Action))]
        public string Caption => Resources.Strings.ResourceManager.GetString($"Keybinding.{Action}.Caption")!;

        [DerivedProperty(nameof(Action))]
        public string Description => Resources.Strings.ResourceManager.GetString($"Keybinding.{Action}.Description")!;

        public bool IsDirectMode
        {
            get => m_isDirectMode;
            set => SetField(ref m_isDirectMode, value);
        }

        internal void SetIsDirectModeInternal(bool value)
        {
            m_isDirectMode = value;
        }

        public bool HasErrors
        {
            get => m_hasErrors;
            set => SetField(ref m_hasErrors, value);
        }

        public static IList<KeybindingViewModel> FromDictionary(KeybindingDictionary items)
        {
            return items.Select(kvp =>
            {
                return new KeybindingViewModel
                {
                    Action = kvp.Key,
                    Pattern = kvp.Value?.Keys,
                    IsDirectMode = kvp.Value?.IsDirectMode ?? false,
                };
            }).ToList();
        }

        public static KeybindingDictionary ToDictionary(IEnumerable<KeybindingViewModel> items)
        {
            return new KeybindingDictionary(
                        items.Select(vm => new KeyValuePair<BindableAction, Keybinding?>(
                            vm.Action,
                            vm.Pattern != null ? new Keybinding(vm.Pattern, vm.IsDirectMode) : null)));
        }
    }
}
