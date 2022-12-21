using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FancyWM.Tests.TestUtilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using WinMan;
using FancyWM.Layouts.Tiling;

namespace FancyWM.Layouts.Tests.Tiling
{
    [TestClass]
    public class SplitPanelNodeTest
    {
        private const int MediumWorkAreaWidth = 1920;
        private const int MediumWorkAreaHeight = 1080;
        private static readonly Rectangle MediumWorkArea = new(0, 0, MediumWorkAreaWidth, MediumWorkAreaHeight);

        private readonly WindowMockFactory m_mockFactory = new();

        [TestMethod]
        public void TestOneWindow()
        {
            var desktop = new DesktopTree
            {
                Root = new SplitPanelNode(),
                WorkArea = MediumWorkArea,
            };

            var nodepadNode = new WindowNode(m_mockFactory.CreateNotepadWindow());
            desktop.Root.Attach(nodepadNode);
            desktop.Measure();
            desktop.Arrange();

            Assert.AreEqual(MediumWorkArea, nodepadNode.ComputedRectangle);
        }

        [TestMethod]
        public void TestTwoWindows()
        {
            var desktop = new DesktopTree
            {
                Root = new SplitPanelNode(),
                WorkArea = MediumWorkArea,
            };

            var nodepadNode = new WindowNode(m_mockFactory.CreateNotepadWindow());
            desktop.Root.Attach(nodepadNode);
            desktop.Measure();
            desktop.Arrange();
            var explorerNode = new WindowNode(m_mockFactory.CreateExplorerWindow());
            desktop.Root.Attach(explorerNode);
            desktop.Measure();
            desktop.Arrange();

            Assert.AreEqual(new Rectangle(0, 0, MediumWorkAreaWidth / 2, MediumWorkAreaHeight), nodepadNode.ComputedRectangle);
            Assert.AreEqual(new Rectangle(MediumWorkAreaWidth / 2, 0, MediumWorkAreaWidth, MediumWorkAreaHeight), explorerNode.ComputedRectangle);
        }


        [TestMethod]
        public void TestThreeWindows()
        {
            var desktop = new DesktopTree
            {
                Root = new SplitPanelNode(),
                WorkArea = MediumWorkArea,
            };

            var nodepadNode = new WindowNode(m_mockFactory.CreateNotepadWindow());
            desktop.Root.Attach(nodepadNode);
            desktop.Measure();
            desktop.Arrange();
            var nodepadNode2 = new WindowNode(m_mockFactory.CreateNotepadWindow());
            desktop.Root.Attach(nodepadNode2);
            desktop.Measure();
            desktop.Arrange();
            var explorerNode = new WindowNode(m_mockFactory.CreateExplorerWindow());
            desktop.Root.Attach(explorerNode);
            desktop.Measure();
            desktop.Arrange();

            Assert.AreEqual(new Rectangle(0, 0, MediumWorkAreaWidth / 3, MediumWorkAreaHeight), nodepadNode.ComputedRectangle);
            Assert.AreEqual(new Rectangle(MediumWorkAreaWidth / 3, 0, MediumWorkAreaWidth / 3 * 2, MediumWorkAreaHeight), nodepadNode2.ComputedRectangle);
            Assert.AreEqual(new Rectangle(MediumWorkAreaWidth / 3 * 2, 0, MediumWorkAreaWidth, MediumWorkAreaHeight), explorerNode.ComputedRectangle);
        }

        [TestMethod]
        public void TestFourWindowsOneLarge()
        {
            var desktop = new DesktopTree
            {
                Root = new SplitPanelNode(),
                WorkArea = MediumWorkArea,
            };

            var nodepadNode = new WindowNode(m_mockFactory.CreateNotepadWindow());
            desktop.Root.Attach(nodepadNode);
            desktop.Measure();
            desktop.Arrange();
            var nodepadNode2 = new WindowNode(m_mockFactory.CreateNotepadWindow());
            desktop.Root.Attach(nodepadNode2);
            desktop.Measure();
            desktop.Arrange();
            var explorerNode = new WindowNode(m_mockFactory.CreateExplorerWindow());
            desktop.Root.Attach(explorerNode);
            desktop.Measure();
            desktop.Arrange();
            var discordNode = new WindowNode(m_mockFactory.CreateDiscordWindow());
            desktop.Root.Attach(discordNode);
            desktop.Measure();
            desktop.Arrange();
        }

        [TestMethod]
        public void TestTwoLargeWindows()
        {
            var desktop = new DesktopTree
            {
                Root = new SplitPanelNode(),
                WorkArea = MediumWorkArea,
            };

            var explorerNode = new WindowNode(m_mockFactory.CreateExplorerWindow());
            desktop.Root.Attach(explorerNode);
            desktop.Measure();
            desktop.Arrange();
            var discordNode = new WindowNode(m_mockFactory.CreateDiscordWindow());
            desktop.Root.Attach(discordNode);
            desktop.Measure();
            desktop.Arrange();
            Assert.ThrowsException<UnsatisfiableFlexConstraintsException>(() =>
            {
                var discordNode2 = new WindowNode(m_mockFactory.CreateDiscordWindow());
                desktop.Root.Attach(discordNode2);
                desktop.Measure();
                desktop.Arrange();
            });
        }

        [TestMethod]
        public void TestWindowGrowsTooLarge()
        {
            var desktop = new DesktopTree
            {
                Root = new SplitPanelNode(),
                WorkArea = MediumWorkArea,
            };

            var explorerNode = new WindowNode(m_mockFactory.CreateExplorerWindow());
            desktop.Root.Attach(explorerNode);
            desktop.Measure();
            desktop.Arrange();
            var explorerNode2 = new WindowNode(m_mockFactory.CreateExplorerWindow());
            desktop.Root.Attach(explorerNode2);
            desktop.Measure();
            desktop.Arrange();
            var discordNode = new WindowNode(m_mockFactory.CreateDiscordWindow());
            desktop.Root.Attach(discordNode);
            desktop.Measure();
            desktop.Arrange();
            // Replace with larger window to simulate the window growing after it was added
            desktop.Root.SetReference(1, new WindowNode(m_mockFactory.CreateDiscordWindow()));
            Assert.ThrowsException<UnsatisfiableFlexConstraintsException>(() =>
            {
                desktop.Measure();
                desktop.Arrange();
            });
        }
    }
}
