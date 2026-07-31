using System;
using System.Runtime.InteropServices;

namespace FancyWM.Utilities
{
    /// <summary>
    /// Thin wrapper around WM_NCHITTEST for classifying whether a window
    /// considers the current cursor position a sizing border or not.
    /// Used at gesture start to distinguish edge-resize from title-bar-move,
    /// since WinMan fires the same EVENT_SYSTEM_MOVESIZESTART for both.
    /// </summary>
    internal static class NcHitTest
    {
        private const int WM_NCHITTEST = 0x0084;

        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int HTBORDER = 18;
        private const int HTSIZE = 4;  // aka HTGROWBOX

        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint SMTO_BLOCK = 0x0001;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern nint SendMessageTimeout(IntPtr hWnd, int msg, nint wParam, nint lParam, uint flags, uint uTimeout, out nint lpdwResult);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private static nint MakeLParam(int x, int y)
            => (x & 0xFFFF) | ((y & 0xFFFF) << 16);

        /// <summary>
        /// Returns true if the window reports that the current cursor position
        /// is over a sizing border or corner (HTLEFT..HTBOTTOMRIGHT, HTBORDER, HTSIZE).
        /// </summary>
        public static bool IsBorderResize(IntPtr hwnd)
        {
            if (!GetCursorPos(out var cursor))
                return false;
            // Cross-process WM_NCHITTEST must not block the gesture thread on a hung
            // window. Abort if the target is unresponsive and treat that as "not a border".
            if (SendMessageTimeout(hwnd, WM_NCHITTEST, 0, MakeLParam(cursor.X, cursor.Y),
                    SMTO_ABORTIFHUNG | SMTO_BLOCK, 100, out var result) == 0)
                return false;
            int code = (int)(result & 0xFFFF);
            return code is HTLEFT or HTRIGHT or HTTOP or HTTOPLEFT or HTTOPRIGHT
                       or HTBOTTOM or HTBOTTOMLEFT or HTBOTTOMRIGHT
                       or HTBORDER or HTSIZE;
        }
    }
}
