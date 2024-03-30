using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

using FancyWM.DllImports;

namespace FancyWM.Utilities
{
    internal sealed class LowLevelMouseHook : IDisposable
    {
        public enum MouseButton
        {
            Left,
            Right,
            Middle,
        }

        public struct ButtonStateChangedEventArgs(LowLevelMouseHook.MouseButton button, bool isPressed, int ptX, int ptY)
        {
            public readonly MouseButton Button = button;
            public readonly bool IsPressed = isPressed;
            public bool Handled = false;
            public readonly int X = ptX;
            public readonly int Y = ptY;
        }

        public delegate void ButtonStateChangedEventHandler(object? sender, ref ButtonStateChangedEventArgs e);

        public event ButtonStateChangedEventHandler? ButtonStateChanged;

        private static readonly TimeSpan RehookIdleInterval = TimeSpan.FromSeconds(5);

        private readonly HOOKPROC m_hookProcDelegate;
        private readonly Thread m_hookThread;
        private HHOOK m_hHook;
        private bool m_disposedValue = false;
        private uint m_hookThreadId;
        private DateTime m_lastActiveTimestamp = DateTime.UtcNow;

        public LowLevelMouseHook()
        {
            m_hookProcDelegate = HookProc;
            m_hookThread = new Thread(HookThreadMessageLoop)
            {
                Name = "LowLevelMouseHookThread",
            };
            m_hookThread.SetApartmentState(ApartmentState.STA);
            m_hookThread.Start();
        }

        private void HookThreadMessageLoop(object? obj)
        {
            // Message queues are lazily created so we force the creation of one by asking for its status
            _ = PInvoke.GetQueueStatus(GetQueueStatus_flags.QS_ALLEVENTS);
            m_hookThreadId = PInvoke.GetCurrentThreadId();

            HINSTANCE hInstance = new(PInvoke.GetModuleHandle(new PCWSTR()));
            m_hHook = PInvoke.SetWindowsHookEx(SetWindowsHookEx_idHook.WH_MOUSE_LL, m_hookProcDelegate, hInstance, 0);
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
                            m_hHook = PInvoke.SetWindowsHookEx(SetWindowsHookEx_idHook.WH_MOUSE_LL, m_hookProcDelegate, hInstance, 0);
                            PInvoke.UnhookWindowsHookEx(oldHHook);
                        }
                    }
                    else if (lpMsg.message == Constants.WM_QUIT)
                    {
                        return;
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

                ButtonStateChangedEventArgs? e;
                switch (wParam.Value)
                {
                    case Constants.WM_LBUTTONDOWN:
                        {
                            var mshs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            e = new ButtonStateChangedEventArgs(MouseButton.Left, true, mshs.pt.x, mshs.pt.y);
                            break;
                        }

                    case Constants.WM_LBUTTONUP:
                        {
                            var mshs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            e = new ButtonStateChangedEventArgs(MouseButton.Left, false, mshs.pt.x, mshs.pt.y);
                            break;
                        }

                    case Constants.WM_RBUTTONDOWN:
                        {
                            var mshs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            e = new ButtonStateChangedEventArgs(MouseButton.Right, true, mshs.pt.x, mshs.pt.y);
                            break;
                        }

                    case Constants.WM_RBUTTONUP:
                        {
                            var mshs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            e = new ButtonStateChangedEventArgs(MouseButton.Right, false, mshs.pt.x, mshs.pt.y);
                            break;
                        }

                    case Constants.WM_MBUTTONDOWN:
                        {
                            var mshs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            e = new ButtonStateChangedEventArgs(MouseButton.Middle, true, mshs.pt.x, mshs.pt.y);
                            break;
                        }

                    case Constants.WM_MBUTTONUP:
                        {
                            var mshs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            e = new ButtonStateChangedEventArgs(MouseButton.Middle, false, mshs.pt.x, mshs.pt.y);
                            break;
                        }
                    default:
                        return PInvoke.CallNextHookEx(new HHOOK(), code, wParam, lParam);
                }

                if (e is ButtonStateChangedEventArgs evt)
                {
                    ButtonStateChanged?.Invoke(this, ref evt);

                    if (evt.Handled)
                    {
                        return new(PInvoke.CallNextHookEx(new HHOOK(), code, wParam, lParam) | 1);
                    }
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

        ~LowLevelMouseHook()
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
