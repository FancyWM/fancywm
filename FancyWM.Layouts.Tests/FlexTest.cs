using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Layouts.Tests
{
    [TestClass]
    public class FlexTest
    {
        [TestMethod]
        public void TestCreation()
        {
            Flex f = new();
            Assert.AreEqual(1, f.ContainerWidth);
            Assert.AreEqual(0, f.MinWidth, .01);
            Assert.AreEqual(0, f.MaxWidth, .01);
            Assert.AreEqual(0, f.UsedWidth, .01);
        }

        [TestMethod]
        public void TestEmptyResize()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            Assert.AreEqual(100, f.ContainerWidth);
            Assert.AreEqual(0, f.MinWidth, .1);
            Assert.AreEqual(0, f.MaxWidth, .1);
            Assert.AreEqual(0, f.UsedWidth, .1);
        }

        [TestMethod]
        public void TestExpandThenAdd()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            Assert.AreEqual(100, f.ContainerWidth, .1);
            Assert.AreEqual(0, f.MinWidth, .1);
            Assert.AreEqual(100, f.MaxWidth, .1);
            Assert.AreEqual(100, f.UsedWidth, .1);
        }

        [TestMethod]
        public void TestAddBeforeExpand()
        {
            Flex f = new();
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.SetContainerWidth(100);
            Assert.AreEqual(50, f[0].Width, .1);
            Assert.AreEqual(50, f[1].Width, .1);
            Assert.AreEqual(100, f.ContainerWidth, .1);
            Assert.AreEqual(0, f.MinWidth, .1);
            Assert.AreEqual(200, f.MaxWidth, .1);
            Assert.AreEqual(100, f.UsedWidth, .1);
        }

        [TestMethod]
        public void TestTwoEqualItems()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.InsertItem(0, minWidth: 50, maxWidth: 100);
            Assert.AreEqual(100, f.ContainerWidth, .1);
            Assert.AreEqual(50, f.MinWidth, .1);
            Assert.AreEqual(200, f.MaxWidth, .1);
            Assert.AreEqual(100, f.UsedWidth, .1);
            Assert.AreEqual(50, f[0].Width, .1);
            Assert.AreEqual(50, f[1].Width, .1);
        }

        [TestMethod]
        public void TestShrinkToFit()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            Assert.AreEqual(100, f.ContainerWidth, .1);
            Assert.AreEqual(0, f.MinWidth, .1);
            Assert.AreEqual(300, f.MaxWidth, .1);
            Assert.AreEqual(100, f.UsedWidth, .1);
            Assert.AreEqual(33.3, f[0].Width, .1);
            Assert.AreEqual(33.3, f[1].Width, .1);
            Assert.AreEqual(33.3, f[2].Width, .1);
        }

        [TestMethod]
        public void TestUnderfull()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            Assert.AreEqual(100, f.ContainerWidth, .1);
            Assert.AreEqual(0, f.MinWidth, .1);
            Assert.AreEqual(60, f.MaxWidth, .1);
            Assert.AreEqual(60, f.UsedWidth, .1);
            Assert.AreEqual(20, f[0].Width, .1);
            Assert.AreEqual(20, f[1].Width, .1);
            Assert.AreEqual(20, f[2].Width, .1);
        }

        [TestMethod]
        public void TestUnderfullThenOverfull()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 90, maxWidth: 10000);
            Assert.AreEqual(100, f.ContainerWidth, .1);
            Assert.AreEqual(90, f.MinWidth, .1);
            Assert.AreEqual(10040, f.MaxWidth, .1);
            Assert.AreEqual(100, f.UsedWidth, .1);
            Assert.AreEqual(90, f[0].Width, .1);
            Assert.AreEqual(5, f[1].Width, .1);
            Assert.AreEqual(5, f[2].Width, .1);
        }

        [TestMethod]
        public void TestConstraintsIdempotent()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 90, maxWidth: 10000);
            Assert.AreEqual(90, f[0].Width, .1);
            f.UpdateConstraints(0, 10, 100);
            Assert.AreEqual(90, f[0].Width, .1);
        }

        [TestMethod]
        public void TestConstraintsCauseShrink()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 90, maxWidth: 10000);
            Assert.AreEqual(90, f[0].Width, .1);
            f.UpdateConstraints(0, 10, 50);
            Assert.AreEqual(50, f[0].Width, .1);
        }

        [TestMethod]
        public void TestConstraintsCauseGrow()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 90, maxWidth: 10000);
            Assert.AreEqual(90, f[0].Width, .1);
            f.UpdateConstraints(0, 95, 100);
            Assert.AreEqual(95, f[0].Width, .1);
        }

        [TestMethod]
        public void TestRaiseMin()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 90, maxWidth: 10000);
            Assert.AreEqual(5, f[1].Width, .1);
            Assert.AreEqual(5, f[2].Width, .1);
            f.UpdateConstraints(1, 8, 20);
            Assert.AreEqual(8, f[1].Width, .1);
            Assert.AreEqual(2, f[2].Width, .1);
        }

        [TestMethod]
        public void TestReduceMax()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 90, maxWidth: 10000);
            Assert.AreEqual(5, f[1].Width, .1);
            f.UpdateConstraints(1, 0, 3);
            Assert.AreEqual(3, f[1].Width, .1);
        }

        [TestMethod]
        public void TestUpdate()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 90, maxWidth: 10000);
            Assert.AreEqual(90, f[0].Width, .1);
            f.UpdateConstraints(0, 90, 10000);
            Assert.AreEqual(90, f[0].Width, .1);
            f.UpdateConstraints(0, 90, 10000);
            Assert.AreEqual(90, f[0].Width, .1);
        }

        [TestMethod]
        public void TestGrowItemUnderfull()
        {
            Flex f = new();
            f.SetContainerWidth(25);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.InsertItem(0, minWidth: 0, maxWidth: 20);
            f.ResizeItem(0, 15);
            Assert.AreEqual(15, f[0].Width, .1);
            Assert.AreEqual(10, f[1].Width, .1);
        }

        [TestMethod]
        public void TestGrowItem()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.ResizeItem(0, 75);
            Assert.AreEqual(75, f[0].Width, .1);
            Assert.AreEqual(25, f[1].Width, .1);
        }

        [TestMethod]
        public void TestShrinkItem()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.InsertItem(0, minWidth: 0, maxWidth: 100);
            f.ResizeItem(0, 25);
            Assert.AreEqual(25, f[0].Width, .1);
            Assert.AreEqual(75, f[1].Width, .1);
        }

        [TestMethod]
        public void TestAddThirdBalanced()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 40, maxWidth: 100);
            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            Assert.AreEqual(50, f[0].Width, .1);
            Assert.AreEqual(50, f[1].Width, .1);

            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            Assert.AreEqual(33.3, f[0].Width, .1);
            Assert.AreEqual(23.3, f[1].Width, .1);
            Assert.AreEqual(43.3, f[2].Width, .1);
        }


        [TestMethod]
        public void TestAddThirdUnbalanced()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 40, maxWidth: 100);
            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            f.ResizeItem(1, 60);
            Assert.AreEqual(40, f[0].Width, .1);
            Assert.AreEqual(60, f[1].Width, .1);

            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            Assert.AreEqual(33.3, f[0].Width, .1);
            Assert.AreEqual(20, f[1].Width, .1);
            Assert.AreEqual(46.6, f[2].Width, .1);
        }

        [TestMethod]
        public void TestAddThirdUnbalanceSmaller()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 40, maxWidth: 100);
            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            f.ResizeItem(0, 60);
            Assert.AreEqual(60, f[0].Width, .1);
            Assert.AreEqual(40, f[1].Width, .1);

            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            Assert.AreEqual(33.3, f[0].Width, .1);
            Assert.AreEqual(26.6, f[1].Width, .1);
            Assert.AreEqual(40, f[2].Width, .1);
        }

        [TestMethod]
        public void TestAddRemoveParadox()
        {
            Flex f = new();
            f.SetContainerWidth(100);
            f.InsertItem(0, minWidth: 40, maxWidth: 100);
            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            Assert.AreEqual(50, f[0].Width, 0.1);
            Assert.AreEqual(50, f[1].Width, 0.1);

            f.InsertItem(0, minWidth: 10, maxWidth: 100);
            Assert.AreEqual(33.3, f[0].Width, 0.1);
            Assert.AreEqual(23.3, f[1].Width, 0.1);
            Assert.AreEqual(43.3, f[2].Width, 0.1);

            f.RemoveItem(2);
            Assert.AreEqual(53.4, f[0].Width, 0.1);
            Assert.AreEqual(46.5, f[1].Width, 0.1);
        }
    }
}
