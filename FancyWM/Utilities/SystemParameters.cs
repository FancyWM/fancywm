using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

using FancyWM.DllImports;

using static FancyWM.DllImports.PInvoke;

namespace FancyWM.Utilities
{
    public class SystemParameters
    {
        public static SystemParameters Instance { get; } = new();

        public bool ActiveWindowTracking
        {
            get
            {
                unsafe
                {
                    uint value = 0;
                    if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETACTIVEWINDOWTRACKING, 0, &value, 0))
                        throw new Win32Exception("Failed to read mouse tracking setting.");
                    return value != 0;
                }
            }
            set
            {
                unsafe
                {
                    void* param = value ? (void*)1u : (void*)0u;
                    if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETACTIVEWINDOWTRACKING, 0, param, SystemParametersInfo_fWinIni.SPIF_SENDCHANGE))
                        throw new Win32Exception("Failed to set mouse tracking enabled state.");
                }
            }
        }

        public bool Animation
        {
            get
            {
                unsafe
                {
                    // ANIMATIONINFO structure: cbSize (4 bytes) + iMinAnimate (4 bytes)
                    int[] animInfo = new int[2];
                    fixed (int* pAnimInfo = animInfo)
                    {
                        // Set size field before calling
                        animInfo[0] = Marshal.SizeOf<int>() * 2;
                        if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETANIMATION, 0, pAnimInfo, 0))
                            throw new Win32Exception("Failed to read animation setting.");
                        return animInfo[1] != 0;
                    }
                }
            }
            set
            {
                unsafe
                {
                    // ANIMATIONINFO structure: cbSize (4 bytes) + iMinAnimate (4 bytes)
                    int[] animInfo = new int[2];
                    fixed (int* pAnimInfo = animInfo)
                    {
                        animInfo[0] = Marshal.SizeOf<int>() * 2;
                        animInfo[1] = value ? 1 : 0;
                        if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETANIMATION, 0, pAnimInfo, SystemParametersInfo_fWinIni.SPIF_SENDCHANGE))
                            throw new Win32Exception("Failed to set animation enabled state.");
                    }
                }
            }
        }

        public bool WindowArranging
        {
            get
            {
                unsafe
                {
                    uint value = 0;
                    if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWINARRANGING, 0, &value, 0))
                        throw new Win32Exception("Failed to read window arranging setting.");
                    return value != 0;
                }
            }
            set
            {
                unsafe
                {
                    uint param = value ? 1u : 0u;
                    if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWINARRANGING, param, null, SystemParametersInfo_fWinIni.SPIF_SENDCHANGE))
                        throw new Win32Exception("Failed to set window arranging enabled state.");
                }
            }
        }

        public uint HungAppTimeout
        {
            get
            {
                unsafe
                {
                    uint value = 0;
                    if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHUNGAPPTIMEOUT, 0, &value, 0))
                        throw new Win32Exception("Failed to read hung app timeout.");
                    return value;
                }
            }
        }
    }
}
