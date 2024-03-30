using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using FancyWM.Models;
using FancyWM.Utilities;

namespace FancyWM.Converters
{
    internal class KeybindingModel
    {
        public required List<string> Keys { get; set; }
        public bool IsDirectMode { get; set; }
    }

    class KeybindingConverter : JsonConverter<KeybindingDictionary>
    {
        private readonly bool m_useDefaults;

        public KeybindingConverter()
        {
            m_useDefaults = true;
        }

        public KeybindingConverter(bool useDefaults)
        {
            m_useDefaults = useDefaults;
        }

        public override KeybindingDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                return ReadLatest(ref reader, options);
            }
            catch (Exception ex)
            {
                try
                {
                    return Read2dot3dot5Compatible(ref reader, options);
                }
                catch (Exception exCompat)
                {
                    throw new AggregateException(ex, exCompat);
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, KeybindingDictionary value, JsonSerializerOptions options)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var (action, keybinding) in value)
            {
                if (keybinding == null)
                {
                    dict.Add(action.ToString(), null);
                    continue;
                }

                var keys = keybinding.Keys.Select(x => x.ToString()).ToArray();
                dict.Add(action.ToString(), new
                {
                    keybinding.IsDirectMode,
                    Keys = keys,
                });
            };
            JsonSerializer.Serialize(writer, dict, options);
        }

        private KeybindingDictionary ReadLatest(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var defaultDict = new KeybindingDictionary(m_useDefaults);
            var dict = JsonSerializer.Deserialize<IDictionary<string, KeybindingModel?>>(ref reader, options) ?? throw new InvalidOperationException();
            foreach (var (keyName, keyBinds) in dict)
            {
                try
                {
                    var key = (BindableAction)Enum.Parse(typeof(BindableAction), keyName, ignoreCase: true);
                    defaultDict[key] = keyBinds == null ? null : new Keybinding(new HashSet<KeyCode>(keyBinds.Keys.Select(x => (KeyCode)Enum.Parse(typeof(KeyCode), x))), keyBinds.IsDirectMode);
                }
                catch (ArgumentException)
                {
                    // Probably revereted from a newer version
                    continue;
                }
            }

            return defaultDict;
        }

        private KeybindingDictionary Read2dot3dot5Compatible(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var defaultDict = new KeybindingDictionary(m_useDefaults);
            var dict = JsonSerializer.Deserialize<IDictionary<string, string[]>>(ref reader, options) ?? throw new InvalidOperationException();
            foreach (var (keyName, keyStrings) in dict)
            {
                try
                {
                    var key = (BindableAction)Enum.Parse(typeof(BindableAction), keyName, ignoreCase: true);
                    var value = new HashSet<KeyCode>(keyStrings.Select(x => (KeyCode)Enum.Parse(typeof(KeyCode), x)));
                    defaultDict[key] = new Keybinding(value, false);
                }
                catch (ArgumentException)
                {
                    // Probably revereted from a newer version
                    continue;
                }
            }

            return defaultDict;
        }
    }
}
