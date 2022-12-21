using System;
using System.Collections.Generic;
using System.Linq;

using FancyWM.Layouts.Tiling;
using FancyWM.Tests.TestUtilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyWM.Tests
{
    [TestClass]
    public class TilingWorkspaceTest
    {
        private readonly VirtualDesktopMockFactory m_desktopFactory = new();
        private readonly WindowMockFactory m_windowFactory = new();

        [TestMethod]
        public void TestAddRemoveDesktop()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            workspace.UnregisterDesktop(desktop);
        }

        [TestMethod]
        public void TestAddDesktopTwice()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            Assert.ThrowsException<ArgumentException>(() =>
            {
                workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            });
        }

        [TestMethod]
        public void TestRemoveMissingDesktop()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            Assert.ThrowsException<ArgumentException>(() =>
            {
                workspace.UnregisterDesktop(desktop);
            });
        }

        [TestMethod]
        public void TestAddWindowNoParent()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
        }

        [TestMethod]
        public void TestRemoveMissingWindow()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            Assert.ThrowsException<ArgumentException>(() =>
            {
                workspace.UnregisterWindow(m_windowFactory.CreateExplorerWindow());
            });
        }

        [TestMethod]
        public void TestAddWindowTwice()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            workspace.RegisterWindow(explorer);
            Assert.ThrowsException<WindowAlreadyRegisteredException>(() =>
            {
                workspace.RegisterWindow(explorer);
            });
        }

        [TestMethod]
        public void TestFindWindow()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            var node = workspace.RegisterWindow(explorer);

            Assert.IsNotNull(workspace.FindWindow(explorer));
        }

        [TestMethod]
        public void TestFindWindowReturnsNull()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            // var node = workspace.RegisterWindow(explorer);

            Assert.IsNull(workspace.FindWindow(explorer));
        }

        [TestMethod]
        public void TestGetFocusIsNull()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            Assert.IsNull(workspace.GetFocus(desktop));
        }

        [TestMethod]
        public void TestSetFocus()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            var node = workspace.RegisterWindow(explorer);

            workspace.SetFocus(node);
            Assert.AreEqual(workspace.GetFocus(desktop), node);
        }

        [TestMethod]
        public void TestGetFocusAdjacentWindowNoFocus()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            var node = workspace.RegisterWindow(explorer);

            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Left));
            });
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Up));
            });
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Right));
            });
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Down));
            });
        }

        [TestMethod]
        public void TestGetFocusAdjacentWindowNoAdjacent()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            var node = workspace.RegisterWindow(explorer);

            workspace.SetFocus(node);

            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Left));
            });
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Up));
            });
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Right));
            });
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Down));
            });
        }

        [TestMethod]
        public void TestGetFocusAdjacentWindowFlat()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            
            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            var explorer3 = m_windowFactory.CreateExplorerWindow();
            var node3 = workspace.RegisterWindow(explorer3);

            var explorer4 = m_windowFactory.CreateExplorerWindow();
            var node4 = workspace.RegisterWindow(explorer4);

            var explorer5 = m_windowFactory.CreateExplorerWindow();
            var node5 = workspace.RegisterWindow(explorer5);

            workspace.SetFocus(node3);

            Assert.AreEqual(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Left), node2);
            Assert.AreEqual(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Right), node4);
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Up));
            });
            Assert.ThrowsException<TilingFailedException>(() =>
            {
                Assert.IsNull(workspace.GetFocusAdjacentWindow(desktop, TilingDirection.Down));
            });
        }

        [TestMethod]
        public void TestGetOriginalPosition()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer = m_windowFactory.CreateExplorerWindow();
            var node = workspace.RegisterWindow(explorer);

            Assert.AreEqual(workspace.GetOriginalPosition(explorer), explorer.Position);
        }

        [TestMethod]
        public void TestGetOriginalPositionNotRegistered()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer = m_windowFactory.CreateExplorerWindow();
            //var node = workspace.RegisterWindow(explorer);

            Assert.ThrowsException<KeyNotFoundException>(() =>
            {
                Assert.AreEqual(workspace.GetOriginalPosition(explorer), explorer.Position);
            });
        }

        [TestMethod]
        public void TestGetTree()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            Assert.IsNotNull(workspace.GetTree(desktop));
        }


        [TestMethod]
        public void TestGetTreeNotRegistered()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            // workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            Assert.IsNull(workspace.GetTree(desktop));
        }


        [TestMethod]
        public void TestHasWindow()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer = m_windowFactory.CreateExplorerWindow();
            var node = workspace.RegisterWindow(explorer);

            Assert.IsTrue(workspace.HasWindow(explorer));
        }

        [TestMethod]
        public void TestHasWindowNotRegistered()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer = m_windowFactory.CreateExplorerWindow();
            //var node = workspace.RegisterWindow(explorer);

            Assert.IsFalse(workspace.HasWindow(explorer));
        }


        [TestMethod]
        public void TestMoveNodeFlat()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            var tree = workspace.GetTree(desktop);
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(node1, node2.ComputedRectangle.Center);

            Assert.AreEqual(node1.Parent.IndexOf(node1), 1);
            Assert.AreEqual(node2.Parent.IndexOf(node2), 0);
        }

        [TestMethod]
        public void TestMoveNodeFlatLong()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            var explorer3 = m_windowFactory.CreateExplorerWindow();
            var node3 = workspace.RegisterWindow(explorer3);

            var explorer4 = m_windowFactory.CreateExplorerWindow();
            var node4 = workspace.RegisterWindow(explorer4);

            var explorer5 = m_windowFactory.CreateExplorerWindow();
            var node5 = workspace.RegisterWindow(explorer5);

            var tree = workspace.GetTree(desktop);
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            void AssertPositions(params TilingNode[] nodes)
            {
                var parent = tree.Root;
                for (int i = 0; i < nodes.Length; i++)
                {
                    Assert.AreEqual(parent.IndexOf(nodes[i]), i);
                }
            }

            AssertPositions(node1, node2, node3, node4, node5);

            workspace.MoveNode(node1, node2.ComputedRectangle.Center);
            tree.Measure();
            tree.Arrange();

            AssertPositions(node2, node1, node3, node4, node5);

            workspace.MoveNode(node3, node2.ComputedRectangle.Center);
            tree.Measure();
            tree.Arrange();

            AssertPositions(node3, node2, node1, node4, node5);

            workspace.MoveNode(node5, node3.ComputedRectangle.Center);
            tree.Measure();
            tree.Arrange();

            AssertPositions(node5, node3, node2, node1, node4);

            workspace.MoveNode(node2, node1.ComputedRectangle.Center);
            tree.Measure();
            tree.Arrange();

            AssertPositions(node5, node3, node1, node2, node4);
        }

        [TestMethod]
        public void TestMoveWindowFlat()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            var tree = workspace.GetTree(desktop);
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.MoveWindow(explorer1, node2.ComputedRectangle.Center, allowNesting: false);

            Assert.AreEqual(node1.Parent.IndexOf(node1), 1);
            Assert.AreEqual(node2.Parent.IndexOf(node2), 0);
        }

        [TestMethod]
        public void TestMoveAfterSameParent()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            var explorer3 = m_windowFactory.CreateExplorerWindow();
            var node3 = workspace.RegisterWindow(explorer3);

            workspace.SetFocus(node1);
            Assert.ThrowsException<ArgumentException>(() =>
            {
                workspace.MoveAfter(node1, node2);
            });
        }

        [TestMethod]
        public void TestMoveBeforeSameParent()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);

            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            var explorer3 = m_windowFactory.CreateExplorerWindow();
            var node3 = workspace.RegisterWindow(explorer3);

            workspace.SetFocus(node1);
            Assert.ThrowsException<ArgumentException>(() =>
            {
                workspace.MoveBefore(node1, node2);
            });
        }

        [TestMethod]
        public void TestPullUpFromChain()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, PanelOrientation.Horizontal);
            var root = workspace.GetTree(desktop).Root;

            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            workspace.WrapInSplitPanel(node1, true);
            node2.Parent.Detach(node2);
            node1.Parent.Attach(node2);
            workspace.WrapInStackPanel(node1);
            node2.Parent.Detach(node2);

            Assert.AreEqual(root.Children.Count, 1);
            var splitPanelNode = (SplitPanelNode)root.Children[0];
            Assert.AreEqual(splitPanelNode.Children.Count, 1);
            var stackPanelNode = (StackPanelNode)splitPanelNode.Children[0];
            Assert.AreEqual(stackPanelNode.Children.Count, 1);
            Assert.AreEqual(stackPanelNode.Children[0], node1);

            workspace.PullUp(node1);
        }
    }
}
