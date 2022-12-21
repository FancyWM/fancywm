using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FancyWM.Utilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests.Utilities
{
    [TestClass]
    public class CollectionsTest
    {
        [TestMethod]
        public void TestAsPairs()
        {
            Assert.IsTrue(Collections.AsPairs(new KeyValuePair<int, int>[] { new(1, 2), new(3, 4) }).SequenceEqual(new[] { (1, 2), (3, 4) }));
        }

        [TestMethod]
        public void TestAsPairsEmpty()
        {
            Assert.IsTrue(Collections.AsPairs(Array.Empty<KeyValuePair<int, int>>()).SequenceEqual(Array.Empty<(int, int)>()));
        }

        [TestMethod]
        public void TestChangesAdd()
        {
            var (addList, removeList, persistList) = Collections.Changes(new[] { 1, 2, 3 }, new[] { 1, 2, 3, 4 });
            Assert.IsTrue(addList.SequenceEqual(new[] { 4 }));
            Assert.IsTrue(!removeList.Any());
            Assert.IsTrue(persistList.SequenceEqual(new[] { 1, 2, 3 }));
        }

        [TestMethod]
        public void TestChangesRemove()
        {
            var (addList, removeList, persistList) = Collections.Changes(new[] { 1, 2, 3 }, new[] { 1, 2 });
            Assert.IsTrue(!addList.Any());
            Assert.IsTrue(removeList.SequenceEqual(new[] { 3 }));
            Assert.IsTrue(persistList.SequenceEqual(new[] { 1, 2 }));
        }

        [TestMethod]
        public void TestChangesAddRemove()
        {
            var (addList, removeList, persistList) = Collections.Changes(new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 1 });
            Assert.IsTrue(addList.SequenceEqual(new[] { 5, 6, 7 }));
            Assert.IsTrue(removeList.SequenceEqual(new[] { 2, 3, 4 }));
            Assert.IsTrue(persistList.SequenceEqual(new[] { 1 }));
        }

        [TestMethod]
        public void TestChangesComparerAddRemove()
        {
            var (addList, removeList, persistList) = Collections.Changes(new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 1 }, EqualityComparer<int>.Default);
            Assert.IsTrue(addList.SequenceEqual(new[] { 5, 6, 7 }));
            Assert.IsTrue(removeList.SequenceEqual(new[] { 2, 3, 4 }));
            Assert.IsTrue(persistList.SequenceEqual(new[] { 1 }));
        }

        [TestMethod]
        public void TestToSequenceComparer()
        {
            var comparer = EqualityComparer<int>.Default.ToSequenceComparer();
            Assert.IsTrue(comparer.Equals(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }));
            Assert.IsFalse(comparer.Equals(new[] { 1 }, new[] { 1, 2, 3 }));
        }
    }
}
