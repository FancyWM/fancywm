using System;
using System.Collections.Generic;
using System.Linq;

using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using WinMan;
using FancyWM.Layouts;
using System.Diagnostics;
using System.Xml.Linq;

namespace FancyWM
{

    internal enum TilingError
    {
        Failed,
        MissingTarget,
        InvalidTarget,
        MissingAdjacentWindow,
        CausesRecursiveNesting,
        ModifiesTopLevelPanel,
        NoValidPlacementExists,
        TargetCannotFit,
        PullsBeyondTopLevelPanel,
        NestingInStackPanel,
    }

    internal class TilingFailedException : InvalidOperationException
    {
        public TilingError FailReason { get; } = TilingError.Failed;

        public TilingFailedException(TilingError reason = TilingError.Failed)
        {
            FailReason = reason;
        }

        public TilingFailedException(string? message, TilingError reason = TilingError.Failed) : base(message)
        {
            FailReason = reason;
        }

        public TilingFailedException(string? message, Exception? innerException, TilingError reason = TilingError.Failed) : base(message, innerException)
        {
            FailReason = reason;
        }
    }

    public class NoValidPlacementExistsException : Exception
    {
    }

    public class WindowAlreadyRegisteredException : Exception
    {
    }

    internal class DesktopState
    {
        public required DesktopTree DesktopTree { get; init; }
        public TilingNode? FocusedNode { get; set; }
    }

    internal class TilingWorkspaceState
    {
        private readonly Dictionary<IVirtualDesktop, DesktopState> m_states = [];

        public IEnumerable<IVirtualDesktop> Desktops => m_states.Keys;
        public IEnumerable<DesktopState> States => m_states.Values;

        public void AddState(IVirtualDesktop virtualDesktop, DesktopState state)
        {
            m_states.Add(virtualDesktop, state);
        }

        public DesktopState? GetState(IVirtualDesktop virtualDesktop)
        {
            return m_states.TryGetValue(virtualDesktop, out var state) ? state : null;
        }

        public DesktopState? GetState(DesktopTree tree)
        {
            return m_states.Where(x => x.Value.DesktopTree == tree).SingleOrDefault().Value;
        }

        public void RemoveState(IVirtualDesktop virtualDesktop)
        {
            if (!m_states.Remove(virtualDesktop))
            {
                throw new ArgumentException("The specified desktop does not exist!");
            }
        }

        public DesktopState? FindByVdm(IWindow window)
        {
            var desktop = m_states.Keys.FirstOrDefault(x => x.HasWindow(window));
            if (desktop == null)
            {
                return null;
            }
            return m_states[desktop];
        }

        public DesktopState? FindByTree(IWindow window)
        {
            var state = m_states.Values.FirstOrDefault(x => x.DesktopTree.FindNode(window) != null);
            return state;
        }
    }

    internal class TilingWorkspace
    {
        private readonly TilingWorkspaceState m_states = new();
        private readonly Dictionary<IWindow, Rectangle> m_originalPositions = [];

        public IEnumerable<DesktopTree> Trees => m_states.States.Select(x => x.DesktopTree);

        public bool AutoCollapse { get; set; } = false;

        public TilingWorkspace()
        {
        }

        public void RegisterDesktop(IVirtualDesktop virtualDesktop, PanelOrientation orientation)
        {
            var tree = new DesktopTree
            {
                Root = new SplitPanelNode { Orientation = orientation }
            };
            m_states.AddState(virtualDesktop, new DesktopState
            {
                DesktopTree = tree,
                FocusedNode = null,
            });
        }

        public void UnregisterDesktop(IVirtualDesktop virtualDesktop)
        {
            m_states.RemoveState(virtualDesktop);
        }

        public WindowNode RegisterWindow(IWindow window, int maxTreeWidth = 100)
        {
            var state = m_states.FindByVdm(window) ?? throw new InvalidWindowReferenceException(window.Handle);
            var focusedNode = state.FocusedNode;

            PanelNode parent;
            if (focusedNode is WindowNode focusedWindow)
            {
                parent = focusedWindow.Parent!;
            }
            else
            {
                parent = state.DesktopTree.Root!;
            }

            if (parent.Children.Where(x => x is not PlaceholderNode).Count() >= maxTreeWidth && parent is SplitPanelNode parentSplit)
            {
                var nodeToSplit = parent.Children.Contains(focusedNode) ? focusedNode! : parent.Children.Last();
                if (nodeToSplit is WindowNode)
                {
                    WrapInSplitPanel(nodeToSplit, vertical: parentSplit.Orientation == PanelOrientation.Horizontal);

                    try
                    {
                        nodeToSplit.Desktop!.Arrange();
                    }
                    catch (UnsatisfiableFlexConstraintsException)
                    {
                        nodeToSplit.Parent!.CollapseIfSingle();
                    }

                    if (!CanFitLossy(nodeToSplit.Parent!, window))
                    {
                        nodeToSplit.Parent!.CollapseIfSingle();
                    }
                }
                parent = nodeToSplit.Parent!;
            }

            return RegisterWindow(window, parent, focusedNode as WindowNode);
        }

        public WindowNode RegisterWindow(IWindow window, PanelNode parent, WindowNode? anchor = null)
        {
            if (m_states.FindByTree(window) != null)
                throw new WindowAlreadyRegisteredException();

            WindowNode newNode = new(window);
            // Try to fit in the same panel as the target
            if (parent is StackPanelNode || CanFitLossy(parent, window))
            {
                parent.Attach(newNode);
                parent.RemovePlaceholders();
            }
            else if (anchor is WindowNode windowChild)
            {
                if (anchor.Parent is StackPanelNode)
                {
                    anchor.Parent.Attach(newNode);
                }
                else
                {
                    // Could we fit the window by stacking with the target?
                    var freeSizeInParent = windowChild.Parent!.ComputeFreeSize();
                    var minSize = window.MinSize.GetValueOrDefault();
                    if (freeSizeInParent.X + windowChild.ComputedRectangle.Width >= minSize.X
                        && freeSizeInParent.Y + windowChild.ComputedRectangle.Height >= minSize.Y)
                    {
                        windowChild.Embed(new StackPanelNode());
                        windowChild.Parent!.Attach(newNode);
                    }
                    else
                    {
                        throw new NoValidPlacementExistsException();
                    }
                }
            }
            else
            {
                throw new NoValidPlacementExistsException();
            }

            // It is important to use []= here, because the window might have been present on a different virtual desktop
            // and added here before it is removed from there (I think). 
            m_originalPositions[window] = window.Position;
            return newNode;
        }

        private static bool CanFitLossy(PanelNode parent, IWindow window)
        {
            if (parent.ComputedRectangle == default)
            {
                return true;
            }

            var minSize = window.MinSize;
            if (minSize.HasValue)
            {
                return CanFitLossy(parent, minSize.Value);
            }

            return true;
        }

        private static bool CanFitLossy(PanelNode parent, Point minSize)
        {
            if (parent.ComputedRectangle == default)
            {
                return true;
            }

            var maxSize = parent.ComputeFreeSize();
            if (minSize.X > maxSize.X || minSize.Y > maxSize.Y)
            {
                return false;
            }

            return true;
        }

        public void UnregisterWindow(IWindow window)
        {
            var state = m_states.FindByTree(window) ?? throw new ArgumentException(null, nameof(window));
            if (state.FocusedNode is WindowNode node && node.WindowReference == window)
            {
                state.FocusedNode = null;
            }

            state.DesktopTree.FindNode(window)!.Remove(cleanup: true, collapse: AutoCollapse);
            m_originalPositions.Remove(window);
        }

        public Rectangle GetOriginalPosition(IWindow window)
        {
            return m_originalPositions[window];
        }

        public DesktopTree? GetTree(IVirtualDesktop desktop)
        {
            return m_states.GetState(desktop)?.DesktopTree;
        }

        public bool HasWindow(IWindow window)
        {
            var state = m_states.FindByTree(window);
            if (state == null)
            {
                return false;
            }
            return state.DesktopTree.FindNode(window) != null;
        }

        public WindowNode? FindWindow(IWindow window)
        {
            var state = m_states.FindByTree(window);
            if (state == null)
            {
                return null;
            }
            return state.DesktopTree.FindNode(window);
        }

        public TilingNode? NodeAtPoint(IVirtualDesktop currentDesktop, Point pt)
        {
            if (m_states.GetState(currentDesktop) is not DesktopState state)
                throw new ArgumentException("Desktop not registered with backend!");

            return state.DesktopTree.Root!.Windows
                .FirstOrDefault(x => x.ComputedRectangle.Contains(pt));
        }

        public void MoveNode(TilingNode node, Point pt, bool allowNesting = true)
        {
            if (node.Parent == null)
                throw new ArgumentException($"Node cannot be a top-level node!", nameof(node));

            if (node.Desktop == null)
                throw new ArgumentException($"Node must be registered with the backend!", nameof(node));

            var nodeAtPoint = node.Desktop.Root!.Windows
                .Where(x => x != node)
                .Concat(node.Desktop.Root.Nodes.Where(x => x.Type == TilingNodeType.Placeholder))
                .FirstOrDefault(x => x.ComputedRectangle.Contains(pt)) ?? node.Desktop.Root!.Nodes
                    .OfType<PanelNode>()
                    .Where(x => x != node)
                    .FirstOrDefault(x => Rectangle.OffsetAndSize(
                        x.ComputedRectangle.Left - x.Padding.Left,
                        x.ComputedRectangle.Top - x.Padding.Top,
                        x.ComputedRectangle.Width + x.Padding.Left + x.Padding.Right,
                        x.Padding.Top).Contains(pt));
            if (nodeAtPoint == null || nodeAtPoint.Parent == null)
                return;

            if (nodeAtPoint.PathToRoot.Contains(node))
                throw new TilingFailedException(TilingError.CausesRecursiveNesting);

            if (nodeAtPoint.PathToRoot.OfType<StackPanelNode>().Any() && node is not WindowNode)
                throw new TilingFailedException(TilingError.NestingInStackPanel);

            if (nodeAtPoint.Type == TilingNodeType.Placeholder)
            {
                var oldParent = node.Parent;
                node.Parent.Detach(node);
                nodeAtPoint.Parent.Attach(node);
                nodeAtPoint.Parent.RemovePlaceholders();
                oldParent.Cleanup(collapse: AutoCollapse);
            }
            else
            {
                if (allowNesting)
                {
                    if (node.Parent != nodeAtPoint.Parent)
                    {
                        // Node moved over another node that NOT a sibling
                        var oldParent = node.Parent;
                        try
                        {
                            if (!MoveNodeTest(node, nodeAtPoint.Parent, pt))
                            {
                                return;
                            }
                            node.Parent.Detach(node);
                            var insertionIndex = FindInsertionIndex(nodeAtPoint, pt);
                            nodeAtPoint.Parent.Attach(insertionIndex, node);
                            oldParent.Cleanup(collapse: AutoCollapse);
                            node.Parent.RemovePlaceholders();
                        }
                        catch (UnsatisfiableFlexConstraintsException)
                        {
                            throw new TilingFailedException(TilingError.TargetCannotFit);
                        }
                    }
                    else if (node.Parent is not StackPanelNode) // Do not rearrange items in stack panel
                    {
                        try
                        {
                            var newPosition = TransferSize(node.ComputedRectangle, nodeAtPoint.ComputedRectangle);
                            if (newPosition.Contains(pt))
                            {
                                // Node moved over another node that IS a sibling
                                var nodeIndex = node.Parent.Children.IndexOf(node);
                                var targetIndex = node.Parent.Children.IndexOf(nodeAtPoint);

                                node.Parent.Move(nodeIndex, targetIndex);
                            }
                        }
                        catch (UnsatisfiableFlexConstraintsException)
                        {
                            throw new TilingFailedException(TilingError.Failed);
                        }
                    }
                }
                else if (node.Parent != nodeAtPoint)
                {
                    try
                    {
                        node.Swap(nodeAtPoint);
                    }
                    catch (UnsatisfiableFlexConstraintsException)
                    {
                        throw new TilingFailedException(TilingError.Failed);
                    }
                }
            }
        }

        private static int FindInsertionIndex(TilingNode nodeAtPoint, Point pt)
        {
            Debug.Assert(nodeAtPoint.Parent != null);
            var insertionIndex = nodeAtPoint.Parent!.IndexOf(nodeAtPoint);
            if (nodeAtPoint.Parent is SplitPanelNode split)
            {
                if (split.Orientation == PanelOrientation.Horizontal)
                {
                    if (nodeAtPoint.ComputedRectangle.Left + nodeAtPoint.ComputedRectangle.Width / 2 < pt.X)
                    {
                        // Right half of the window in a horizontal panel
                        return insertionIndex + 1;
                    }
                }
                else if (nodeAtPoint.ComputedRectangle.Top + nodeAtPoint.ComputedRectangle.Height / 2 < pt.Y)
                {
                    // Lower half of the window in a vertical panel
                    return insertionIndex + 1;
                }
            }
            else if (nodeAtPoint.Parent is StackPanelNode stack)
            {
                return stack.Children.Count;
            }
            return insertionIndex;
        }

        internal void WrapInSplitPanel(TilingNode node, bool vertical)
        {
            node.Parent?.RemovePlaceholders();
            var isOnlyChild = node.Parent?.Parent != null && node.Parent.Children.Count == 1;
            if (!isOnlyChild && node.Ancestors.OfType<StackPanelNode>().Any())
            {
                throw new TilingFailedException(TilingError.NestingInStackPanel);
            }

            if (node.Parent == null)
                throw new TilingFailedException(TilingError.ModifiesTopLevelPanel);
            // Parent is not the top-level panel and this is the only child
            if (isOnlyChild)
            {
                SwapPanels(node.Parent, new SplitPanelNode
                {
                    Orientation = vertical ? PanelOrientation.Vertical : PanelOrientation.Horizontal,
                });
            }
            else
            {
                node.Embed(new SplitPanelNode
                {
                    Orientation = vertical ? PanelOrientation.Vertical : PanelOrientation.Horizontal,
                });
            }
        }

        internal void WrapInStackPanel(TilingNode node)
        {
            node.Parent?.RemovePlaceholders();
            var isOnlyChild = node.Parent?.Parent != null && node.Parent.Children.Count == 1;

            if (!isOnlyChild && node.Ancestors.OfType<StackPanelNode>().Any())
            {
                throw new TilingFailedException(TilingError.NestingInStackPanel);
            }

            if (!isOnlyChild && node.Nodes.Where(x => x is not WindowNode).Any())
            {
                throw new TilingFailedException(TilingError.NestingInStackPanel);
            }

            if (node.Parent == null)
                throw new TilingFailedException(TilingError.ModifiesTopLevelPanel);

            if (isOnlyChild)
            {
                SwapPanels(node.Parent, new StackPanelNode());
            }
            else
            {
                node.Embed(new StackPanelNode());
            }
        }

        public void MoveBefore(TilingNode node, TilingNode nodeBefore)
        {
            MoveTo(node, nodeBefore, beforeAnchor: true);
        }

        public void MoveAfter(TilingNode node, TilingNode nodeAfter)
        {
            MoveTo(node, nodeAfter, beforeAnchor: false);
        }

        private void MoveTo(TilingNode node, TilingNode nodeAnchor, bool beforeAnchor)
        {
            if (node.Parent == null)
                throw new ArgumentException($"Node cannot be a top-level node!", nameof(node));

            if (node.Desktop == null)
                throw new ArgumentException($"Node must be registered with the backend!", nameof(node));

            if (nodeAnchor.Parent == null)
                throw new ArgumentException($"Node cannot be a top-level node!", nameof(nodeAnchor));

            if (nodeAnchor.Desktop == null)
                throw new ArgumentException($"Node must be registered with the backend!", nameof(nodeAnchor));

            if (node.Parent == nodeAnchor.Parent)
                throw new ArgumentException($"Nodes must have different parents!", nameof(nodeAnchor));

            var index = nodeAnchor.Parent.IndexOf(nodeAnchor);
            var oldParent = node.Parent;
            node.Parent.Detach(node);
            oldParent.Cleanup(collapse: AutoCollapse);
            nodeAnchor.Parent.Attach(beforeAnchor ? index : index + 1, node);
            nodeAnchor.Parent.RemovePlaceholders();
        }

        private bool MoveNodeTest(TilingNode node, PanelNode newParentNode, Point pt)
        {
            Debug.Assert(node.Desktop != null);
            Debug.Assert(node.Parent != null);
            Debug.Assert(newParentNode.Desktop != null);
            Debug.Assert(newParentNode.Desktop != null);
            Debug.Assert(node.Desktop == newParentNode.Desktop);

            var rootClone = (PanelNode)node.Desktop.Root!.Clone();

            var nodeClone = rootClone.Nodes.First(x => x.GenerationID == node.GenerationID);
            var newParentClone = (PanelNode)rootClone.Nodes.First(x => x.GenerationID == newParentNode.GenerationID);
            var testTree = new DesktopTree
            {
                Root = rootClone,
                WorkArea = node.Desktop.WorkArea,
            };

            var nodeCloneParent = nodeClone.Parent!;
            var newParentIsAncestor = nodeCloneParent.Ancestors.Contains(newParentClone);

            nodeCloneParent.Detach(nodeClone);
            nodeCloneParent.Cleanup(collapse: AutoCollapse);

            if (newParentClone.Desktop == null && newParentIsAncestor)
            {
                // The new parent node got completely detached because it was an ancestor
                // of the existing node and apparently it was a change of 1-child panels.
                return true;
            }

            newParentClone.Attach(nodeClone);
            newParentClone.RemovePlaceholders();
            testTree.Measure();
            testTree.Arrange();

            return newParentClone.ComputedRectangle.Contains(pt);
        }

        private static Rectangle TransferSize(Rectangle a, Rectangle b)
        {
            var newCenter = b.Center;
            var width = a.Width;
            var height = a.Height;

            return new Rectangle(newCenter.X - width / 2, newCenter.Y - height / 2, newCenter.X + width / 2, newCenter.Y + height / 2);
        }

        public void MoveWindow(IWindow window, Point pt, bool allowNesting)
        {
            var state = m_states.FindByTree(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            var sourceNode = state.DesktopTree.FindNode(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            MoveNode(sourceNode, pt, allowNesting);
        }


        public (Rectangle preArrange, Rectangle postArrange) MockMoveWindow(IWindow window, Point pt, bool allowNesting)
        {
            var state = m_states.FindByTree(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            var sourceNode = state.DesktopTree.FindNode(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            return MockMoveNode(sourceNode, pt, allowNesting);
        }

        public (Rectangle preArrange, Rectangle postArrange) MockMoveNode(TilingNode sourceNode, Point pt, bool allowNesting)
        {
            var desktop = sourceNode.Desktop!;
            var rootClone = (PanelNode)desktop.Root!.Clone();

            var sourceNodeClone = rootClone.Nodes.First(x => x.GenerationID == sourceNode.GenerationID);
            var testTree = new DesktopTree
            {
                Root = rootClone,
                WorkArea = desktop.WorkArea,
            };

            MoveNode(sourceNodeClone, pt, allowNesting);

            var unconstrainedParentClone = (PanelNode)sourceNodeClone.Parent!.Clone();

            try
            {
                testTree.Arrange();
            }
            catch (UnsatisfiableFlexConstraintsException)
            {
                throw new TilingFailedException(TilingError.NoValidPlacementExists);
            }

            foreach (var node in unconstrainedParentClone.Nodes)
            {
                node.ClearConstraints();
            }
            unconstrainedParentClone.Padding = new();
            try
            {
                unconstrainedParentClone.Arrange(new RectangleF(unconstrainedParentClone.ComputedRectangle));
            }
            catch (UnsatisfiableFlexConstraintsException)
            {
                throw new TilingFailedException(TilingError.NoValidPlacementExists);
            }
            var unconstrainedSourceNodeClone = unconstrainedParentClone.Nodes.First(x => x.GenerationID == sourceNode.GenerationID);

            return (unconstrainedSourceNodeClone.ComputedRectangle, sourceNodeClone.ComputedRectangle);
        }

        public void ResizeWindow(IWindow window, Rectangle newPosition, Rectangle oldPosition)
        {
            var state = m_states.FindByTree(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            var node = state.DesktopTree.FindNode(window);
            if (node != null)
            {
                if (newPosition.Width != oldPosition.Width)
                {
                    SplitPanelNode? p = node.Ancestors
                        .Select(x => x as SplitPanelNode)
                        .Where(x => x != null)
                        .FirstOrDefault(x => x!.Orientation == PanelOrientation.Horizontal);

                    if (p != null)
                    {
                        GrowDirection direction = newPosition.Left == oldPosition.Left
                            ? GrowDirection.TowardsEnd
                            : newPosition.Right == oldPosition.Right
                                ? GrowDirection.TowardsStart
                                : GrowDirection.Both;
                        var child = p.Children.First(x => x.Windows.Contains(node));
                        p.Resize(child, newPosition.Width, direction);
                    }
                }

                if (newPosition.Height != oldPosition.Height)
                {
                    SplitPanelNode? p = node.Ancestors
                        .Select(x => x as SplitPanelNode)
                        .Where(x => x != null)
                        .FirstOrDefault(x => x!.Orientation == PanelOrientation.Vertical);

                    if (p != null)
                    {
                        GrowDirection direction = newPosition.Top == oldPosition.Top
                            ? GrowDirection.TowardsEnd
                            : newPosition.Bottom == oldPosition.Bottom
                                ? GrowDirection.TowardsStart
                                : GrowDirection.Both;
                        var child = p.Children.First(x => x.Windows.Contains(node));
                        p.Resize(child, newPosition.Height, direction);
                    }
                }
            }
        }

        public TilingNode? GetFocus(IVirtualDesktop currentDesktop)
        {
            if (m_states.GetState(currentDesktop) is not DesktopState state)
                throw new ArgumentException("Desktop not registered with backend!");

            return state.FocusedNode;
        }

        public WindowNode GetFocusAdjacentWindow(IVirtualDesktop currentDesktop, TilingDirection direction)
        {
            var focusedNode = GetFocus(currentDesktop) ?? throw new TilingFailedException(TilingError.MissingTarget);
            WindowNode? adjacentWindow = focusedNode.GetAdjacentWindow(direction) ?? throw new TilingFailedException(TilingError.MissingAdjacentWindow);
            return adjacentWindow;
        }

        public (TilingNode, WindowNode) GetFocusAndAdjacentWindow(IVirtualDesktop currentDesktop, TilingDirection direction)
        {
            var focusedNode = GetFocus(currentDesktop) ?? throw new TilingFailedException(TilingError.MissingTarget);
            WindowNode? adjacentWindow = focusedNode.GetAdjacentWindow(direction) ?? throw new TilingFailedException(TilingError.MissingAdjacentWindow);
            return (focusedNode, adjacentWindow);
        }

        public void SetFocus(TilingNode node)
        {
            if (m_states.GetState(node.Desktop!) is not DesktopState state)
                throw new ArgumentException("Desktop not registered with backend!");

            state.FocusedNode = node;
        }

        public void SetFocus(IWindow window)
        {
            var state = m_states.FindByTree(window) ?? throw new ArgumentException("Window not registered with backend!");
            var node = state.DesktopTree.FindNode(window);
            Debug.Assert(node != null);

            SetFocus(node);
        }

        public void UnsetFocus(IWindow window)
        {
            var state = m_states.FindByTree(window);
            if (state == null)
                return;

            if (state.FocusedNode is WindowNode node && node.WindowReference == window)
                state.FocusedNode = null;
        }

        public void UnsetFocus(IVirtualDesktop desktop)
        {
            if (m_states.GetState(desktop) is not DesktopState state)
                throw new ArgumentException("Desktop not registered with backend!");
            state.FocusedNode = null;
        }

        public void SwapPanels(PanelNode panel, PanelNode newPanel)
        {
            if (panel.Parent == null)
                throw new TilingFailedException(TilingError.ModifiesTopLevelPanel);

            var grandparent = panel.Parent;

            grandparent.Attach(newPanel);
            panel.Swap(newPanel);

            var children = new List<TilingNode>(panel.Children);
            foreach (var node in children)
            {
                panel.Detach(node);
                newPanel.Attach(node);
            }

            panel.Cleanup(collapse: AutoCollapse);
        }

        bool CanFit(PanelNode parent, TilingNode child)
        {
            if (child.PathToRoot.Contains(parent))
            {
                try
                {
                    _ = MoveNodeTest(child, parent, new Point());
                    return true;
                }
                catch (UnsatisfiableFlexConstraintsException)
                {
                    return false;
                }
#if !DEBUG
                catch (Exception)
                {
                    return false;
                }
#endif
            }
            else
            {
                return CanFitLossy(parent, child.MinSize);
            }
        }

        public void PullUp(TilingNode node)
        {
            if (node.Parent == null)
                throw new TilingFailedException(TilingError.InvalidTarget);

            if (node.Parent.Parent == null)
                throw new TilingFailedException(TilingError.PullsBeyondTopLevelPanel);

            // First grandparent that we can fit in
            var grandparent = node.Parent.PathToRoot.Skip(1).OfType<PanelNode>().FirstOrDefault(x => CanFit(x, node)) ?? throw new TilingFailedException(TilingError.TargetCannotFit);
            var oldParent = node.Parent;
            var index = grandparent.IndexOf(node.Parent);

            node.Parent.Detach(node);
            grandparent.Attach(index, node);

            oldParent.Cleanup(collapse: AutoCollapse);
        }
    }
}
