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

    internal class ByProcessNameMatcher : IWindowMatcher
    {
        public string ProcessName { get; }

        public ByProcessNameMatcher(string processName)
        {
            ProcessName = processName;
        }

        public bool Matches(IWindow window)
        {
            return MatchHelpers.IsMatch(window.GetCachedProcessName(), ProcessName);
        }
    }

    internal class ByClassNameMatcher : IWindowMatcher
    {
        public string ClassName { get; }

        public ByClassNameMatcher(string className)
        {
            ClassName = className;
        }

        public bool Matches(IWindow window)
        {
            return (window is WinMan.Windows.Win32Window w) && MatchHelpers.IsMatch(w.ClassName, ClassName);
        }
    }
}
