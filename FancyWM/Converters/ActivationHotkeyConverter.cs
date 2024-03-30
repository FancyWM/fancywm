using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using FancyWM.Models;
using FancyWM.Utilities;

namespace FancyWM.Converters
{
    class ActivationHotkeyConverter : JsonConverter<ActivationHotkey>
    {
        static string Serialize(ActivationHotkey hk)
        {
            KeyCode[] keys = [..hk.ModifierKeys, hk.Key];
            return string.Join('_', keys);
        }

        public override ActivationHotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? str = reader.GetString() ?? throw new FormatException();
            var hotkey = ActivationHotkey.AllowedHotkeys.FirstOrDefault(x => Serialize(x) == str) ?? ActivationHotkey.AllowedHotkeys[0];
            return hotkey;
        }

        public override void Write(Utf8JsonWriter writer, ActivationHotkey value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Serialize(value));
        }
    }
}