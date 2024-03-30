using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

using FancyWM.DllImports;

namespace FancyWM.Utilities
{
    internal sealed class LowLevelKeyboardHook : IDisposable
    {
        public struct KeyStateChangedEventArgs(KeyCode keyCode, bool isPressed)
        {
            public readonly KeyCode KeyCode = keyCode;
            public readonly bool IsPressed = isPressed;
            public bool Handled = false;
        }

        public delegate void KeyStateChangedEventHandler(object? sender, ref KeyStateChangedEventArgs e);

        public event KeyStateChangedEventHandler? KeyStateChanged;

        private static readonly TimeSpan RehookIdleInterval = TimeSpan.FromSeconds(5);

        private const uint LLKHF_UP = 0x80;

        private readonly HOOKPROC m_hookProcDelegate;
        private readonly Thread m_hookThread;
        private HHOOK m_hHook;
        private bool m_disposedValue = false;
        private uint m_hookThreadId;
        private DateTime m_lastActiveTimestamp = DateTime.UtcNow;

        public LowLevelKeyboardHook()
        {
            m_hookProcDelegate = HookProc;
            m_hookThread = new Thread(HookThreadMessageLoop)
            {
                Name = "LowLevelKeyboardHookThread",
            };
            m_hookThread.SetApartmentState(ApartmentState.STA);
            m_hookThread.Start();
        }

        private void HookThreadMessageLoop(object? obj)
        {
            // Message queues are lazily created so we forcec the creation of one by asking for its status
            PInvoke.GetQueueStatus(GetQueueStatus_flags.QS_ALLEVENTS);
            m_hookThreadId = PInvoke.GetCurrentThreadId();
            HINSTANCE hInstance = new(PInvoke.GetModuleHandle(new PCWSTR()));
            m_hHook = PInvoke.SetWindowsHookEx(SetWindowsHookEx_idHook.WH_KEYBOARD_LL, m_hookProcDelegate, hInstance, 0);
            if (m_hHook == IntPtr.Zero)
            {
                throw new Win32Exception("Failed to set global WH_KEYBOARD_LL!");
            }

            nuint timerId = PInvoke.SetTimer(new HWND(), 0, 1000, null);
            try
            {
                while (PInvoke.GetMessage(out MSG lpMsg, new(0), 0, 0))
                {
                    if (lpMsg.message == Constants.WM_TIMER)
                    {
                        if (DateTime.UtcNow - m_lastActiveTimestamp > RehookIdleInterval)
                        {
                            m_lastActiveTimestamp = DateTime.UtcNow;
                            // Rehook if elapsed
                            HHOOK oldHHook = m_hHook;
                            m_hHook = PInvoke.SetWindowsHookEx(SetWindowsHookEx_idHook.WH_KEYBOARD_LL, m_hookProcDelegate, hInstance, 0);
                            PInvoke.UnhookWindowsHookEx(oldHHook);
                        }
                    }
                    else
                    {
                        PInvoke.DispatchMessage(in lpMsg);
                    }
                }
            }
            catch (ThreadInterruptedException)
            {
            }
            finally
            {
                PInvoke.KillTimer(new HWND(), timerId);
            }

            if (!PInvoke.UnhookWindowsHookEx(m_hHook))
            {
                throw new Win32Exception("Failed to unset the global WH_KEYBOARD_LL!");
            }
        }

        private LRESULT HookProc(int code, WPARAM wParam, LPARAM lParam)
        {
            if (code >= Constants.HC_ACTION)
            {
                // Update the timestamp
                m_lastActiveTimestamp = DateTime.UtcNow;

                var kbhs = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                KeyCode keyCode = (KeyCode)kbhs.vkCode;
                bool isPressed = (kbhs.flags & LLKHF_UP) == 0;
                var e = new KeyStateChangedEventArgs(keyCode, isPressed);
                KeyStateChanged?.Invoke(this, ref e);

                if (e.Handled)
                {
                    return new(PInvoke.CallNextHookEx(new HHOOK(), code, wParam, lParam) | 1);
                }
            }

            return PInvoke.CallNextHookEx(new HHOOK(), code, wParam, lParam);
        }

        private void Dispose(bool disposing)
        {
            if (!m_disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects) if any
                }

                while (m_hookThreadId == 0)
                    Thread.Yield();
                PInvoke.PostThreadMessage(m_hookThreadId, Constants.WM_QUIT, new(0), new(0));
                m_hookThread.Join(1000);

                m_disposedValue = true;
            }
        }

        ~LowLevelKeyboardHook()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
