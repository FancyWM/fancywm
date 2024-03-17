using System;
using System.Text;
using System.Windows.Input;

namespace FancyWM.Utilities
{
    internal static class KeyDescriptions
    {
        public static string GetDescription(KeyCode key)
        {
            return key switch
            {
                KeyCode.LeftCtrl => "Ctrl",
                KeyCode.RightCtrl => "Right Ctrl",
                KeyCode.ShiftKey => "⇧",
                KeyCode.LeftShift => "⇧",
                KeyCode.RightShift => "Right ⇧",
                KeyCode.Menu => "Alt",
                KeyCode.LeftAlt => "Alt",
                KeyCode.RightAlt => "Right Alt",
                KeyCode.LWin => "⊞",
                KeyCode.RWin => "Right ⊞",
                KeyCode.Escape => "Esc",
                KeyCode.Return => "Enter",
                KeyCode.Back => "Backspace",
                KeyCode.Apps => "Context Menu",
                KeyCode.Oemtilde => "`",
                KeyCode.OemMinus => "-",
                KeyCode.OemPlus => "=",
                KeyCode.Add => "NumPad +",
                KeyCode.Subtract => "NumPad -",
                KeyCode.Divide => "NumPad /",
                KeyCode.Multiply => "NumPad *",
                KeyCode.OemPipe => "|",
                KeyCode.OemQuotes => "'",
                KeyCode.OemQuestion => "/",
                KeyCode.OemBackslash => "\\",
                KeyCode.Decimal => "NumPad .",
                KeyCode.OemSemicolon => ";",
                KeyCode.OemOpenBrackets => "[",
                KeyCode.OemCloseBrackets => "]",
                KeyCode.OemComma => ",",
                KeyCode.OemPeriod => ".",
                KeyCode.D0 => "0",
                KeyCode.D1 => "1",
                KeyCode.D2 => "2",
                KeyCode.D3 => "3",
                KeyCode.D4 => "4",
                KeyCode.D5 => "5",
                KeyCode.D6 => "6",
                KeyCode.D7 => "7",
                KeyCode.D8 => "8",
                KeyCode.D9 => "9",
                KeyCode.NumPad0 => "NumPad 0",
                KeyCode.NumPad1 => "NumPad 1",
                KeyCode.NumPad2 => "NumPad 2",
                KeyCode.NumPad3 => "NumPad 3",
                KeyCode.NumPad4 => "NumPad 4",
                KeyCode.NumPad5 => "NumPad 5",
                KeyCode.NumPad6 => "NumPad 6",
                KeyCode.NumPad7 => "NumPad 7",
                KeyCode.NumPad8 => "NumPad 8",
                KeyCode.NumPad9 => "NumPad 9",
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
