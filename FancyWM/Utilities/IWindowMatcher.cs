using System;
using System.Text.RegularExpressions;

using WinMan;

namespace FancyWM.Utilities
{
    internal interface IWindowMatcher
    {
        bool Matches(IWindow window);
    }

    internal class MatchHelpers
    {
        public static bool IsMatch(string input, string pattern)
        {
            if (string.Equals(input, pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (pattern.Length == 0)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    internal class ByProcessNameMatcher(string processName) : IWindowMatcher
    {
        public string ProcessName { get; } = processName;

        public bool Matches(IWindow window)
        {
            return MatchHelpers.IsMatch(window.GetCachedProcessName(), ProcessName);
        }
    }

    internal class ByClassNameMatcher(string className) : IWindowMatcher
    {
        public string ClassName { get; } = className;

        public bool Matches(IWindow window)
        {
            return (window is WinMan.Windows.Win32Window w) && MatchHelpers.IsMatch(w.ClassName, ClassName);
        }
    }
}
