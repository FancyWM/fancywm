using System;
using System.ComponentModel;

using FancyWM.DllImports;


namespace FancyWM.Utilities
{
    internal class WindowCopyDataHelper
    {

        internal static byte[] Receive(IntPtr lParam)
        {
            unsafe
            {
                var cds = *(COPYDATASTRUCT*)(void*)lParam;
                var length = (int)cds.cbData;
                var bytes = new byte[length];
                switch (cds.dwData)
                {
                    case 0:
                        for (int i = 0; i < length; i++)
                        {
                            bytes[i] = ((byte*)(void*)cds.lpData)[i];
                        }
                        return bytes;

                    default:
                        throw new ArgumentException();
                }
            }
        }

        internal static void Send(IntPtr hwnd, byte[] bytes)
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    COPYDATASTRUCT cds = new COPYDATASTRUCT();
                    cds.dwData = 0;
                    cds.cbData = (uint)bytes.Length;
                    cds.lpData = ptr;
                    nuint result;
                    var ret = PInvoke.SendMessageTimeout(new(hwnd), Constants.WM_COPYDATA, new(0), (LPARAM)(nint)(&cds), SendMessageTimeout_fuFlags.SMTO_NORMAL, 3000, &result);
                    if (ret == 0)
                    {
                        throw new Win32Exception();
                    }
                }
            }
        }
    }
}
