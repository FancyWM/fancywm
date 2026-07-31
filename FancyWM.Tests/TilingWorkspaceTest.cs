using System;
using System.Collections.Generic;
using System.Linq;

using FancyWM;
using FancyWM.Layouts.Tiling;
using FancyWM.Models;
using FancyWM.Tests.TestUtilities;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using WinMan;

namespace FancyWM.Tests
{
    [TestClass]
    public class TilingWorkspaceTest
    {
        private readonly VirtualDesktopMockFactory m_desktopFactory = new();
        private readonly WindowMockFactory m_windowFactory = new();
        private readonly Rectangle m_workarea = new(0, 0, 1920, 1080);

        /// <summary>
        /// Point over <paramref name="target"/> in the left edge band (outside the center stack zone), so
        /// <see cref="TilingWorkspace.ClassifyDropZone"/> yields <see cref="TilingWorkspace.DropZone.Left"/> and
        /// insertion index matches the legacy flat <c>Move()</c> reorder tests.
        /// </summary>
        private static Point SiblingReorderPointOver(WindowNode target)
        {
            var r = target.ComputedRectangle;
            return new Point(r.Left + (int)(r.Width * 0.28), r.Top + r.Height / 2);
        }

        [TestMethod]
        public void TestAddRemoveDesktop()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
            workspace.UnregisterDesktop(desktop);
        }

        [TestMethod]
        public void TestAddDesktopTwice()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
            Assert.ThrowsException<ArgumentException>(() =>
            {
                workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
        }

        [TestMethod]
        public void TestRemoveMissingWindow()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            workspace.RegisterWindow(explorer);

            Assert.IsNotNull(workspace.FindWindow(explorer));
        }

        [TestMethod]
        public void TestFindWindowReturnsNull()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
            var explorer = m_windowFactory.CreateExplorerWindow();
            // var node = workspace.RegisterWindow(explorer);

            Assert.IsNull(workspace.FindWindow(explorer));
        }

        [TestMethod]
        public void TestGetFocusIsNull()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
            Assert.IsNull(workspace.GetFocus(desktop));
        }

        [TestMethod]
        public void TestSetFocus()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var explorer = m_windowFactory.CreateExplorerWindow();
            workspace.RegisterWindow(explorer);

            Assert.AreEqual(workspace.GetOriginalPosition(explorer), explorer.Position);
        }

        [TestMethod]
        public void TestGetOriginalPositionNotRegistered()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            Assert.IsNotNull(workspace.GetTree(desktop));
        }


        [TestMethod]
        public void TestGetTreeNotRegistered()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            // workspace.RegisterDesktop(desktop, m_rectangle, PanelOrientation.Horizontal);

            Assert.IsNull(workspace.GetTree(desktop));
        }


        [TestMethod]
        public void TestHasWindow()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var explorer = m_windowFactory.CreateExplorerWindow();
            workspace.RegisterWindow(explorer);

            Assert.IsTrue(workspace.HasWindow(explorer));
        }

        [TestMethod]
        public void TestHasWindowNotRegistered()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var explorer = m_windowFactory.CreateExplorerWindow();
            //var node = workspace.RegisterWindow(explorer);

            Assert.IsFalse(workspace.HasWindow(explorer));
        }


        [TestMethod]
        public void TestMoveNodeFlat()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var explorer1 = m_windowFactory.CreateExplorerWindow();
            var node1 = workspace.RegisterWindow(explorer1);

            var explorer2 = m_windowFactory.CreateExplorerWindow();
            var node2 = workspace.RegisterWindow(explorer2);

            var tree = workspace.GetTree(desktop);
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(node1, SiblingReorderPointOver(node2));

            Assert.AreEqual(node1.Parent.IndexOf(node1), 1);
            Assert.AreEqual(node2.Parent.IndexOf(node2), 0);
        }

        [TestMethod]
        public void TestMoveNodeFlatLong()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

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

            workspace.MoveNode(node1, SiblingReorderPointOver(node2));
            tree.Measure();
            tree.Arrange();

            AssertPositions(node2, node1, node3, node4, node5);

            workspace.MoveNode(node3, SiblingReorderPointOver(node2));
            tree.Measure();
            tree.Arrange();

            AssertPositions(node3, node2, node1, node4, node5);

            workspace.MoveNode(node5, SiblingReorderPointOver(node3));
            tree.Measure();
            tree.Arrange();

            AssertPositions(node5, node3, node2, node1, node4);

            // Swap adjacent siblings (center hit would stack if allowNesting were true)
            workspace.MoveNode(node2, node1.ComputedRectangle.Center, allowNesting: false);
            tree.Measure();
            tree.Arrange();

            AssertPositions(node5, node3, node1, node2, node4);
        }

        [TestMethod]
        public void TestMoveWindowFlat()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

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
        public void TestRegisterWindowOverflowStackCreatesStackPanel()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), 2, OverflowPlacementStrategy.Stack);
            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), 2, OverflowPlacementStrategy.Stack);
            var overflowNode = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), 2, OverflowPlacementStrategy.Stack);

            Assert.IsInstanceOfType(overflowNode.Parent, typeof(StackPanelNode));
        }

        [TestMethod]
        public void TestRegisterWindowOverflowStackReusesExistingStack()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), 2, OverflowPlacementStrategy.Stack);
            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), 2, OverflowPlacementStrategy.Stack);
            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), 2, OverflowPlacementStrategy.Stack);
            workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), 2, OverflowPlacementStrategy.Stack);

            var tree = workspace.GetTree(desktop)!;
            var stacks = CollectStackPanelsUnderRoot(tree.Root!).ToList();
            Assert.AreEqual(2, stacks.Count, "with max width 2, overflow stacking should use two stack columns when possible");
            var counts = stacks.Select(s => s.Children.Count(c => c is WindowNode)).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { 2, 2 }, counts, "new windows should spread across stacks (least-loaded first)");
        }

        private static IEnumerable<StackPanelNode> CollectStackPanelsUnderRoot(PanelNode root)
        {
            foreach (var ch in root.Children)
            {
                foreach (var sp in EnumerateStackPanelsInSubtree(ch))
                    yield return sp;
            }
        }

        private static IEnumerable<StackPanelNode> EnumerateStackPanelsInSubtree(TilingNode node)
        {
            if (node is StackPanelNode sp)
            {
                yield return sp;
                yield break;
            }

            if (node is PanelNode p)
            {
                foreach (var c in p.Children)
                {
                    foreach (var nested in EnumerateStackPanelsInSubtree(c))
                        yield return nested;
                }
            }
        }

        private static int CountSplitPanels(TilingNode node)
        {
            var count = node is SplitPanelNode ? 1 : 0;
            if (node is PanelNode panel)
            {
                foreach (var child in panel.Children)
                {
                    count += CountSplitPanels(child);
                }
            }

            return count;
        }

        [TestMethod]
        public void TestMoveNodeCenterCreatesStackDrop()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var source = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var target = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var tree = workspace.GetTree(desktop)!;
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(source, target.ComputedRectangle.Center);

            Assert.IsInstanceOfType(source.Parent, typeof(StackPanelNode));
            Assert.AreSame(source.Parent, target.Parent);
        }

        [TestMethod]
        public void TestMoveNodeCenterReordersWithinSameStack()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var a = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var b = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var tree = workspace.GetTree(desktop)!;
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(a, b.ComputedRectangle.Center);

            var c = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(c, a.ComputedRectangle.Center);

            Assert.IsInstanceOfType(a.Parent, typeof(StackPanelNode));
            var stack = (StackPanelNode)a.Parent!;
            Assert.AreEqual(3, stack.Children.Count(x => x is WindowNode));
            Assert.IsInstanceOfType(b.Parent, typeof(StackPanelNode));
            Assert.AreSame(stack, b.Parent);

            workspace.MoveNode(b, a.ComputedRectangle.Center);

            Assert.AreSame(stack, b.Parent);
            Assert.AreEqual(3, stack.Children.Count(x => x is WindowNode));
        }

        [TestMethod]
        public void TestClassifyDropZoneCornerIsNeutral()
        {
            var r = new Rectangle(0, 0, 1000, 800);
            var corner = (int)(Math.Min(r.Width, r.Height) * TilingWorkspace.DropZoneCornerFraction);
            var pt = new Point(r.Left + corner / 2, r.Top + corner / 2);
            Assert.AreEqual(TilingWorkspace.DropZone.Neutral, TilingWorkspace.ClassifyDropZone(r, pt));
        }

        [TestMethod]
        public void TestMoveNodeCrossParentNeutralCornerKeepsSameSplitParent()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
            var w1 = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var w2 = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var tree = workspace.GetTree(desktop)!;
            tree.WorkArea = new Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();
            var rootSplit = (SplitPanelNode)w1.Parent!;
            Assert.AreSame(rootSplit, w2.Parent);
            var r2 = w2.ComputedRectangle;
            var corner = (int)(Math.Min(r2.Width, r2.Height) * TilingWorkspace.DropZoneCornerFraction);
            var ptNeutral = new Point(r2.Left + corner / 2, r2.Top + corner / 2);
            Assert.AreEqual(TilingWorkspace.DropZone.Neutral, TilingWorkspace.ClassifyDropZone(r2, ptNeutral));
            workspace.MoveNode(w1, ptNeutral);
            tree.Measure();
            tree.Arrange();
            Assert.AreSame(rootSplit, w1.Parent);
            Assert.AreSame(rootSplit, w2.Parent);
        }

        [TestMethod]
        public void TestMoveNodeFromOneStackToAnotherEdgeDropDoesNotThrow()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var w1 = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var w2 = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var tree = workspace.GetTree(desktop)!;
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(w1, w2.ComputedRectangle.Center);
            tree.Measure();
            tree.Arrange();

            Assert.IsInstanceOfType(w1.Parent, typeof(StackPanelNode));
            var rootSplit = (SplitPanelNode)w1.Parent!.Parent!;
            var w3 = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), rootSplit);
            var w4 = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow(), rootSplit);
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(w3, w4.ComputedRectangle.Center);
            tree.Measure();
            tree.Arrange();

            var stack2 = (StackPanelNode)w3.Parent!;
            Assert.IsInstanceOfType(w1.Parent, typeof(StackPanelNode));
            Assert.AreSame(stack2, w4.Parent);
            Assert.AreNotSame(w1.Parent, stack2);

            // Edge of target (not center stack band): cross-parent path must not WrapInSplitPanel the stacked target.
            workspace.MoveNode(w1, SiblingReorderPointOver(w4));
            tree.Measure();
            tree.Arrange();

            Assert.AreSame(stack2, w1.Parent);
            CollectionAssert.Contains(stack2.Children.ToList(), w1);
        }

        [TestMethod]
        public void TestMoveNodeDropOnStackPrefersFocusedTabNotAdjacentSoloTile()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var a = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var b = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var tree = workspace.GetTree(desktop)!;
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.MoveNode(a, b.ComputedRectangle.Center);

            var solo = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var incoming = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            tree.Measure();
            tree.Arrange();

            workspace.SetFocus(b);
            var pt = b.ComputedRectangle.Center;
            workspace.MoveNode(incoming, pt);

            Assert.IsInstanceOfType(incoming.Parent, typeof(StackPanelNode));
            var stack = (StackPanelNode)incoming.Parent!;
            Assert.AreSame(a.Parent, incoming.Parent);
            Assert.AreEqual(3, stack.Children.Count(static c => c is WindowNode));
            Assert.IsInstanceOfType(solo.Parent, typeof(SplitPanelNode));
        }

        [TestMethod]
        public void TestMoveNodeSimpleTwoWindowSplitChainReusesAncestorInsteadOfNesting()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

            var a = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var b = workspace.RegisterWindow(m_windowFactory.CreateExplorerWindow());
            var tree = workspace.GetTree(desktop)!;
            tree.WorkArea = new WinMan.Rectangle(0, 0, 2000, 2000);
            tree.Measure();
            tree.Arrange();

            workspace.WrapInSplitPanel(a, vertical: true);
            tree.Measure();
            tree.Arrange();

            var rootSplit = (SplitPanelNode)tree.Root!;
            var splitCountBefore = CountSplitPanels(rootSplit);

            workspace.MoveNode(b, SiblingReorderPointOver(a));
            tree.Measure();
            tree.Arrange();

            var splitCountAfter = CountSplitPanels(rootSplit);
            Assert.AreEqual(splitCountBefore, splitCountAfter, "simple two-window split chains should not gain extra nested splits");
        }

        [TestMethod]
        public void TestMoveAfterSameParent()
        {
            var workspace = new TilingWorkspace();
            var desktop = m_desktopFactory.CreateVirtualDesktop();
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);

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
            workspace.RegisterDesktop(desktop, m_workarea, PanelOrientation.Horizontal);
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

        [TestMethod]
        public void TestRealWorkspaceInstance()
        {
            for (int i = 0; i < 2; i++)
            {
                using var workspace = new WinMan.Windows.Win32Workspace();
                workspace.Open();
                Assert.IsTrue(workspace.VirtualDesktopManager.CanManageVirtualDesktops);
                Assert.IsTrue(workspace.DisplayManager.Displays.Count > 0);
            }
        }
    }
}
