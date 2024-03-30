using System;
using System.Diagnostics;

using Microsoft.Win32;

namespace FancyWM.Utilities
{
    internal static class WinAero
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, int Msg, IntPtr wParam, string? lParam, uint fuFlags, uint uTimeout, IntPtr lpdwResult);

        private static readonly IntPtr HWND_BROADCAST = new(0xffff);
        private const int WM_SETTINGCHANGE = 0x1a;
        private const int SMTO_ABORTIFHUNG = 0x0002;

        public static bool IsAeroSnapEnabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key == null)
                    return false;
                try
                {
                    var state = (string?)key.GetValue("WindowArrangementActive", "0");
                    return state == "1";
                }
                catch (InvalidCastException)
                {
                    return false;
                }
            }
            set
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true) ?? throw new PlatformNotSupportedException(@"Could not locate key HKCU\Control Panel\Desktop!");
                key.SetValue("WindowArrangementActive", value ? "1" : "0");
                var ps = Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = "USER32.DLL,UpdatePerUserSystemParameters 1, True",
                    UseShellExecute = true,
                }) ?? throw new InvalidOperationException("Could not refresh explorer.exe settings from registry!");
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, null, SMTO_ABORTIFHUNG, 100, IntPtr.Zero);
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Policy", SMTO_ABORTIFHUNG, 100, IntPtr.Zero);
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, new(1), null, SMTO_ABORTIFHUNG, 100, IntPtr.Zero);
                SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, new(1), "Policy", SMTO_ABORTIFHUNG, 100, IntPtr.Zero);

                ps.WaitForExit();
            }
        }
    }
}
