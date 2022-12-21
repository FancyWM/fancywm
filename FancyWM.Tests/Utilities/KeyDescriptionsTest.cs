
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Utilities.Tests
{
    [TestClass]
    public class KeyDescriptionsTest
    {
        [TestMethod]
        public void TestCaseConversion()
        {
            Assert.AreEqual("Browser Back", KeyDescriptions.GetDescription(System.Windows.Input.Key.BrowserBack));
        }
    }
}
