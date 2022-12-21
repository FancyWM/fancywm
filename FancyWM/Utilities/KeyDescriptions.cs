using System;
using System.Text;
using System.Windows.Input;

namespace FancyWM.Utilities
{
    internal static class KeyDescriptions
    {
        public static string GetDescription(Key key)
        {
            return key switch
            {
                Key.LeftCtrl => "Ctrl",
                Key.RightCtrl => "Right Ctrl",
                Key.LeftShift => "⇧",
                Key.RightShift => "Right ⇧",
                Key.LeftAlt => "Alt",
                Key.RightAlt => "Right Alt",
                Key.System => "Alt",
                Key.LWin => "⊞",
                Key.RWin => "Right ⊞",
                Key.Escape => "Esc",
                Key.Return => "Enter",
                Key.Back => "Backspace",
                Key.Apps => "Context Menu",
                Key.OemTilde => "`",
                Key.OemMinus => "-",
                Key.OemPlus => "=",
                Key.Add => "NumPad +",
                Key.Subtract => "NumPad -",
                Key.Divide => "NumPad /",
                Key.Multiply => "NumPad *",
                Key.OemPipe => "|",
                Key.OemQuotes => "'",
                Key.OemQuestion => "/",
                Key.OemBackslash => "\\",
                Key.Decimal => "NumPad .",
                Key.OemSemicolon => ";",
                Key.OemOpenBrackets => "[",
                Key.OemCloseBrackets => "]",
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                Key.D0 => "0",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                Key.D6 => "6",
                Key.D7 => "7",
                Key.D8 => "8",
                Key.D9 => "9",
                Key.NumPad0 => "NumPad 0",
                Key.NumPad1 => "NumPad 1",
                Key.NumPad2 => "NumPad 2",
                Key.NumPad3 => "NumPad 3",
                Key.NumPad4 => "NumPad 4",
                Key.NumPad5 => "NumPad 5",
                Key.NumPad6 => "NumPad 6",
                Key.NumPad7 => "NumPad 7",
                Key.NumPad8 => "NumPad 8",
                Key.NumPad9 => "NumPad 9",
                _ => SplitPascalCase(key.ToString()),
            };
        }

        private static string SplitPascalCase(string s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            if (s.Length < 2)
            {
                return s;
            }

            var sb = new StringBuilder();
            sb.Append(char.ToUpperInvariant(s[0]));
            for (int i = 1; i < s.Length; ++i)
            {
                char c = s[i];
                if (char.IsUpper(c))
                {
                    sb.Append(' ');
                    sb.Append(c);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
