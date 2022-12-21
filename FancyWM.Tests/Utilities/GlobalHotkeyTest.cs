using System;
using System.Threading.Tasks;

using FancyWM.Tests.TestUtilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Utilities.Tests
{
    [TestClass]
    public class GlobalHotkeyTest
    {
        [TestMethod]
        public async Task TestRegisterUnregister()
        {
            await Applications.WithAppContextAsync(async () =>
            {
                var ghk = new GlobalHotkey(IntPtr.Zero, DllImports.RegisterHotKey_fsModifiersFlags.MOD_ALT | DllImports.RegisterHotKey_fsModifiersFlags.MOD_CONTROL, KeyCode.Home);
                ghk.Register();
                ghk.Unregiser();
            });
        }
    }
}
