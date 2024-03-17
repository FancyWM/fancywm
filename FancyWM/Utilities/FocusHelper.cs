using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using FancyWM.DllImports;

namespace FancyWM.Utilities
{
    internal static class FocusHelper
    {
        public static bool ForceActivate(IntPtr hwnd)
        {
            if (PInvoke.SetForegroundWindow(new(hwnd)))
            {
                return true;
            }

            var tcs = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                // This call creates a message queue for the thread.
                PInvoke.PeekMessage(out MSG _, new HWND(-1), 0, 0, PeekMessage_wRemoveMsg.PM_NOREMOVE);
                uint tid = PInvoke.GetCurrentThreadId();

                IntPtr hwndFore = PInvoke.GetForegroundWindow();
                uint tidFore;
                unsafe
                {
                    tidFore = PInvoke.GetWindowThreadProcessId(new(hwndFore), null);
                }

                if (!PInvoke.AttachThreadInput(tid, tidFore, true))
                {
                    tcs.SetResult(false);
                    return;
                }

                bool focused = PInvoke.SetForegroundWindow(new(hwnd));
                if (!focused)
                {
                    // Simulate two Alt keypresses to disable the lock.
                    INPUT[] inp = new INPUT[4];
                    inp[0].type = inp[1].type = inp[2].type = inp[3].type = INPUT_typeFlags.INPUT_KEYBOARD;
                    inp[0].Anonymous.ki.wVk = inp[1].Anonymous.ki.wVk = inp[2].Anonymous.ki.wVk = inp[3].Anonymous.ki.wVk = (ushort)Constants.VK_MENU;
                    inp[0].Anonymous.ki.dwFlags = inp[2].Anonymous.ki.dwFlags = keybd_eventFlags.KEYEVENTF_EXTENDEDKEY;
                    inp[1].Anonymous.ki.dwFlags = inp[3].Anonymous.ki.dwFlags = keybd_eventFlags.KEYEVENTF_EXTENDEDKEY | keybd_eventFlags.KEYEVENTF_KEYUP;
                    PInvoke.SendInput(inp, Marshal.SizeOf<INPUT>());

                    focused = PInvoke.SetForegroundWindow(new(hwnd));
                }
                PInvoke.AttachThreadInput(tid, tidFore, false);

                tcs.SetResult(focused);
            });
            thread.Start();
            thread.Join(100);
            return tcs.Task.Result;
        }
    }
}
