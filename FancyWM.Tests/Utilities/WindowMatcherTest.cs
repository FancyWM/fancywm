
using FancyWM.Tests.TestUtilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Utilities.Tests
{
    [TestClass]
    public class WindowMatcherTest
    {
        private readonly WindowMockFactory m_mockFactory = new();

        [TestMethod]
        public void TestByProcessNameExact()
        {
            var matcher = new ByProcessNameMatcher("explorer");
            Assert.IsTrue(matcher.Matches(m_mockFactory.CreateExplorerWindow()));
        }

        [TestMethod]
        public void TestByProcessNameInExact()
        {
            var matcher = new ByProcessNameMatcher("ExPlOrEr");
            Assert.IsTrue(matcher.Matches(m_mockFactory.CreateExplorerWindow()));
        }


        [TestMethod]
        public void TestByProcessNameFails()
        {
            var matcher = new ByProcessNameMatcher("explorer.something");
            Assert.IsFalse(matcher.Matches(m_mockFactory.CreateExplorerWindow()));
        }
    }
}
