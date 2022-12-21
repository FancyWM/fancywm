using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.Win32;

namespace FancyWM.Utilities
{
    internal class SystemWallpaper
    {
        private const uint MAX_PATH = 260;
        private const uint SPI_GETDESKTOPWALLPAPER = 0x0073;

        [Flags]
        public enum SetWindowPosFlags : uint
        {
            SWP_ASYNCWINDOWPOS = 0x4000,
            SWP_DEFERERASE = 0x2000,
            SWP_DRAWFRAME = 0x0020,
            SWP_FRAMECHANGED = 0x0020,
            SWP_HIDEWINDOW = 0x0080,
            SWP_NOACTIVATE = 0x0010,
            SWP_NOCOPYBITS = 0x0100,
            SWP_NOMOVE = 0x0002,
            SWP_NOOWNERZORDER = 0x0200,
            SWP_NOREDRAW = 0x0008,
            SWP_NOREPOSITION = 0x0200,
            SWP_NOSENDCHANGING = 0x0400,
            SWP_NOSIZE = 0x0001,
            SWP_NOZORDER = 0x0004,
            SWP_SHOWWINDOW = 0x0040,
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, [Out] char[] pvParam, uint fWinIni);

        public string? WallpaperPath { get; init; }

        public byte[]? RGB { get; init; }

        public static SystemWallpaper GetCurrent()
        {
            if (GetDesktopWallpaper() is string path)
            {
                return new SystemWallpaper
                {
                    WallpaperPath = path,
                };
            }

            if (Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors")?.GetValue("Background", null) is string colorValue)
            {
                var channelValues = colorValue.Split(' ');
                if (channelValues.Length == 3)
                {
                    try
                    {
                        byte[] channels = channelValues.Select(byte.Parse).ToArray();
                        return new SystemWallpaper
                        {
                            RGB = channels,
                        };
                    }
                    catch (Exception e) when (e is FormatException || e is OverflowException)
                    {
                        // Badly formatted color.
                    }
                }
            }

            return new SystemWallpaper();
        }

        private static string? GetDesktopWallpaper()
        {
            char[] pvParam = new char[(int)MAX_PATH];
            if (!SystemParametersInfo(SPI_GETDESKTOPWALLPAPER, MAX_PATH, pvParam, 0))
            {
                throw new Win32Exception("SPI_GETDESKTOPWALLPAPER");
            }
            if (pvParam[0] == '\0')
            {
                return null;
            }
            return new string(pvParam, 0, Strlen(pvParam));
        }

        private static int Strlen(char[] s)
        {
            int i = 0;
            while (s[i] != '\0')
                i++;
            return i;
        }
    }
}
