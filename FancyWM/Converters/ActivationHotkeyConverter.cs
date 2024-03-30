using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using FancyWM.Models;

namespace FancyWM.Converters
{
    class ActivationHotkeyConverter : JsonConverter<ActivationHotkey>
    {
        public override ActivationHotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? str = reader.GetString() ?? throw new FormatException();
            var hotkey = ActivationHotkey.AllowedHotkeys.FirstOrDefault(x => $"{x.KeyA}_{x.KeyB}" == str) ?? ActivationHotkey.AllowedHotkeys[0];
            return hotkey;
        }

        public override void Write(Utf8JsonWriter writer, ActivationHotkey value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"{value.KeyA}_{value.KeyB}");
        }
    }
}