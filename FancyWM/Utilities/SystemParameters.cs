using System;
using System.ComponentModel;

using FancyWM.DllImports;

using static FancyWM.DllImports.PInvoke;

namespace FancyWM.Utilities
{
    public static class SystemParameters
    {
        public static bool ActiveWindowTracking
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
                    if (!SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETACTIVEWINDOWTRACKING, 0, param, SystemParametersInfo_fWinIni.SPIF_UPDATEINIFILE | SystemParametersInfo_fWinIni.SPIF_SENDCHANGE))
                        throw new Win32Exception("Failed to set mouse tracking enabled state.");
                }
            }
        }
    }
}
