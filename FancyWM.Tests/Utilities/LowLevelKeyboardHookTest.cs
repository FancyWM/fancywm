
using Microsoft.VisualStudio.TestTools.UnitTesting;

using FancyWM.Utilities;
using System.Threading.Tasks;

namespace FancyWM.Tests.Utilities
{
    [TestClass]
    public class LowLevelKeyboardHookTest
    {
        [TestMethod]
        public void TestNewDispose()
        {
            var llkbh = new LowLevelKeyboardHook();
            llkbh.Dispose();
        }
    }
}
