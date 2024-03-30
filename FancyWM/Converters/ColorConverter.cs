using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace FancyWM.Converters
{
    class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var colorCode = reader.GetString() ?? throw new InvalidOperationException();
            if (!int.TryParse(colorCode.Replace("#", ""), NumberStyles.HexNumber, null, out int rgba))
            {
                throw new FormatException("The numeric part of the color value is invalid!");
            }

            int r, g, b, a;
            if (colorCode.Length == 7)
            {
                r = rgba >> 16;
                g = (rgba >> 8) & 0xFF;
                b = rgba & 0xFF;
                a = 0xFF;
            }
            else if (colorCode.Length == 9)
            {
                r = rgba >> 24;
                g = (rgba >> 16) & 0xFF;
                b = (rgba >> 8) & 0xFF;
                a = rgba & 0xFF;
            }
            else
            {
                throw new FormatException("Color code is not the expected 7 or 9 characters in length (including #)!");
            }

            return Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);
        }

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStringValue($"#{value.R:X2}{value.G:X2}{value.B:X2}{value.A:X2}");
        }
    }
}
