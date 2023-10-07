using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

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
                var hwnd = new WindowInteropHelper(Application.Current.MainWindow).EnsureHandle();
                var ghk = new GlobalHotkey(hwnd, DllImports.RegisterHotKey_fsModifiersFlags.MOD_ALT | DllImports.RegisterHotKey_fsModifiersFlags.MOD_CONTROL, KeyCode.Home);
                ghk.Register();
                ghk.Unregiser();
            });
        }
    }
}
