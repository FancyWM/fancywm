using Microsoft.VisualStudio.TestTools.UnitTesting;

using FancyWM.Utilities;

namespace FancyWM.Utilities.Tests
{
    [TestClass]
    public class EasingFunctionTest
    {
        [TestMethod]
        public void TestSimple()
        {
            var e = EasingFunction.Create(_ => 0.5);
            Assert.AreEqual(0.5, e.Evaluate(0));
            Assert.AreEqual(0.5, e.Evaluate(1));
        }

        [TestMethod]
        public void TestClamp()
        {
            var e = EasingFunction.Create(progress => progress * 2);
            Assert.AreEqual(0, e.Evaluate(0));
            Assert.AreEqual(1, e.Evaluate(0.5));
            Assert.AreEqual(2, e.Evaluate(2));
        }
    }
}
