using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FancyWM.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests
{
    [TestClass]
    public class TilingManagerTest
    {
        [TestMethod]
        public void TestAsPairs()
        {
            Assert.IsTrue(Collections.AsPairs(new KeyValuePair<int, int>[] { new(1, 2), new(3, 4) }).SequenceEqual(new[] { (1, 2), (3, 4) }));
        }
    }
}
