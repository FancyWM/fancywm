using FancyWM.DllImports;

namespace FancyWM.Utilities
{
    internal static class ExplorerFeature
    {
        public static bool HasVirtualDesktopTooltip()
        {
            HWND it = new();
            while ((it = PInvoke.FindWindowEx(new(), it, "XamlExplorerHostIslandWindow", null)) != default)
            {
                WINDOWS_EX_STYLE exStyle = unchecked((WINDOWS_EX_STYLE)PInvoke.GetWindowLong(it, GetWindowLongPtr_nIndex.GWL_EXSTYLE));
                if (exStyle.HasFlag(WINDOWS_EX_STYLE.WS_EX_TOPMOST) && exStyle.HasFlag(WINDOWS_EX_STYLE.WS_EX_NOACTIVATE))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
