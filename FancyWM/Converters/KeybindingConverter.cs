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
                    return Read_2_15_0(ref reader, options);
                }
                catch (Exception exCompat)
                {
                    try
                    {
                        return Read_2_3_5(ref reader, options);
                    }
                    catch (Exception ex2dot3dot5Compat)
                    {
                        throw new AggregateException(ex, exCompat, ex2dot3dot5Compat);
                    }
                }
            }
        }

        public override void Write(Utf8JsonWriter writer, KeybindingDictionary value, JsonSerializerOptions options)
        {
            var dict = new Dictionary<string, string?>();
            foreach (var (action, keybinding) in value)
            {
                if (keybinding == null)
                {
                    dict.Add(action.ToString(), null);
                    continue;
                }

                var keys = string.Join('+', keybinding.Keys.Select(x => x.ToString()));
                if (!keybinding.IsDirectMode)
                {
                    keys = "Activation " + keys;
                }
                dict.Add(action.ToString(), keys);
            }
            JsonSerializer.Serialize(writer, dict, options);
        }

        const KeyCode Activation = unchecked((KeyCode)0xFFFFFFFF);

        private KeyCode ParseKey(string s)
        {
            if (s.ToLowerInvariant() == "activation")
            {
                return Activation;
            }
            return (KeyCode)Enum.Parse(typeof(KeyCode), s, ignoreCase: true);
        }

        private KeybindingDictionary ReadLatest(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var defaultDict = new KeybindingDictionary(m_useDefaults);
            var dict = JsonSerializer.Deserialize<IDictionary<string, string?>>(ref reader, options) ?? throw new InvalidOperationException();
            foreach (var (actionName, keyBinds) in dict)
            {
                try
                {
                    var action = (BindableAction)Enum.Parse(typeof(BindableAction), actionName, ignoreCase: true);
                    if (keyBinds == null)
                    {
                        defaultDict[action] = null;
                        continue;
                    }

                    var chordStrings = keyBinds.Split(' ');

                    var chords = chordStrings.Select(chord => chord.Split('+').Select(ParseKey).ToHashSet()).ToArray();

                    // Allowed forms:
                    // - keyA+keyB
                    // - activation keyA+keyB
                    if (chords.Length == 0)
                    {
                        continue;
                    }
                    if (chords.Length == 1 && !chords[0].Contains(Activation))
                    {
                        defaultDict[action] = new Keybinding(chords[0], isDirectMode: true);
                    }
                    else if (chords.Length == 2 && chords[0].Count == 1 && chords[0].Contains(Activation) && !chords[1].Contains(Activation))
                    {
                        defaultDict[action] = new Keybinding(chords[1], isDirectMode: false);
                    }
                }
                catch (ArgumentException)
                {
                    // Probably revereted from a newer version
                    continue;
                }
            }

            return defaultDict;
        }

        private KeybindingDictionary Read_2_15_0(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var defaultDict = new KeybindingDictionary(m_useDefaults);
            var dict = JsonSerializer.Deserialize<IDictionary<string, KeybindingModel?>>(ref reader, options) ?? throw new InvalidOperationException();
            foreach (var (keyName, keyBinds) in dict)
            {
                try
                {
                    var key = (BindableAction)Enum.Parse(typeof(BindableAction), keyName, ignoreCase: true);
                    defaultDict[key] = keyBinds == null ? null : new Keybinding(new HashSet<KeyCode>(keyBinds.Keys.Select(x => (KeyCode)Enum.Parse(typeof(KeyCode), x, ignoreCase: true))), keyBinds.IsDirectMode);
                }
                catch (ArgumentException)
                {
                    // Probably revereted from a newer version
                    continue;
                }
            }

            return defaultDict;
        }

        private KeybindingDictionary Read_2_3_5(ref Utf8JsonReader reader, JsonSerializerOptions options)
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
