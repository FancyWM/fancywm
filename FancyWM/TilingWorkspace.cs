using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Xml.Linq;

using FancyWM.Layouts;
using FancyWM.Layouts.Tiling;
using FancyWM.Models;
using FancyWM.Utilities;

using Windows.Devices.Enumeration;

using WinMan;

using Serilog;

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
        internal enum DropZone
        {
            Center,
            Left,
            Right,
            Top,
            Bottom,
            /// <summary>Corner bands: insert/reorder without wrapping the target in a new split/stack.</summary>
            Neutral,
        }

        /// <summary>Fraction of min(w,h) for corner <see cref="DropZone.Neutral"/> (matches highlight geometry).</summary>
        internal const double DropZoneCornerFraction = 0.14;

        /// <summary>
        /// Same-parent sibling-drag behavior; see <see cref="SiblingDragMode"/>. Driven by the
        /// user setting (<see cref="ITilingServiceSettings.SiblingDragMode"/>) via TilingService.
        /// Implemented in <see cref="MoveNode"/>'s same-parent branch.
        /// </summary>
        public SiblingDragMode SiblingDrag { get; set; } = SiblingDragMode.Hybrid;

        private readonly TilingWorkspaceState m_states = new();
        private readonly Dictionary<IWindow, Rectangle> m_originalPositions = [];
        private readonly ILogger? m_logger;

        public IEnumerable<DesktopTree> Trees => m_states.States.Select(x => x.DesktopTree);

        public bool AutoCollapse { get; set; } = false;

        public TilingWorkspace(ILogger? logger = null)
        {
            m_logger = logger;
        }

        public PanelNode CreateRoot(PanelOrientation orientation)
        {
            // return new LayoutFunctionNode(new GridLayout(8));
            // return new LayoutFunctionNode(new RatioLayout(0.5, 8));
            return new SplitPanelNode { Orientation = orientation };
        }

        public void RegisterDesktop(IVirtualDesktop virtualDesktop, Rectangle workArea, PanelOrientation orientation)
        {
            var tree = new DesktopTree
            {
                Root = CreateRoot(orientation),
                WorkArea = workArea,
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

        public WindowNode RegisterWindow(
            IWindow window,
            int maxTreeWidth = 100,
            OverflowPlacementStrategy overflowStrategy = OverflowPlacementStrategy.Stack)
        {
            var state = GetValidatedState(window);
            var focusedNode = state.FocusedNode;
            var parent = ResolveParent(state, focusedNode);
            parent = ResolveParentForOverflow(state, parent, focusedNode, window, maxTreeWidth, overflowStrategy);
            return RegisterWindow(window, parent, focusedNode as WindowNode);
        }

        public WindowNode RegisterWindow(IWindow window, PanelNode parent, WindowNode? anchor = null)
        {
            if (m_states.FindByTree(window) != null)
                throw new WindowAlreadyRegisteredException();

            var newNode = new WindowNode(window);
            AttachToParent(newNode, parent);
            m_originalPositions[window] = window.Position;
            return newNode;
        }

        private DesktopState GetValidatedState(IWindow window)
        {
            var state = m_states.FindByVdm(window)
                ?? throw new InvalidWindowReferenceException(window.Handle);

            if (state.DesktopTree.FindNode(window) is WindowNode)
                throw new WindowAlreadyRegisteredException();

            return state;
        }

        private static PanelNode ResolveParent(DesktopState state, TilingNode? focusedNode)
            => focusedNode is WindowNode focusedWindow
                ? focusedWindow.Parent ?? state.DesktopTree.Root!
                : state.DesktopTree.Root!;

        private PanelNode ResolveParentForOverflow(
            DesktopState state,
            PanelNode parent,
            TilingNode? focusedNode,
            IWindow window,
            int maxTreeWidth,
            OverflowPlacementStrategy overflowStrategy)
        {
            var shouldAttemptOverflowPlacement = IsAtMaxWidth(parent, maxTreeWidth) || !CanFitLossy(parent, window);
            if (!shouldAttemptOverflowPlacement)
            {
                return parent;
            }

            var nodeToSplit = SelectNodeToSplit(parent, focusedNode);
            return overflowStrategy switch
            {
                OverflowPlacementStrategy.Stack => ResolveStackOverflowParent(state, parent, focusedNode, nodeToSplit, window, maxTreeWidth),
                OverflowPlacementStrategy.Vertical => ResolveSplitOverflowParent(nodeToSplit, vertical: true, window),
                OverflowPlacementStrategy.Horizontal => ResolveSplitOverflowParent(nodeToSplit, vertical: false, window),
                _ => parent,
            };
        }

        private static bool IsAtMaxWidth(PanelNode parent, int maxTreeWidth)
            => parent.Children.Count(x => x is not PlaceholderNode) >= maxTreeWidth;

        private static TilingNode SelectNodeToSplit(PanelNode parent, TilingNode? focusedNode)
            => parent.Children.Contains(focusedNode) ? focusedNode! : parent.Children.Last();

        private PanelNode ResolveSplitOverflowParent(TilingNode nodeToSplit, bool vertical, IWindow window)
        {
            if (nodeToSplit is not WindowNode)
            {
                return nodeToSplit.Parent!;
            }

            WrapInSplitPanel(nodeToSplit, vertical);
            ArrangeWithFallback(nodeToSplit);

            if (!CanFitLossy(nodeToSplit.Parent!, window))
            {
                nodeToSplit.Parent!.CollapseIfSingle();
            }

            return nodeToSplit.Parent!;
        }

        private PanelNode ResolveStackOverflowParent(
            DesktopState state,
            PanelNode originalParent,
            TilingNode? focusedNode,
            TilingNode nodeToStack,
            IWindow window,
            int maxStackPanels)
        {
            var stacks = CollectStackPanels(state).ToList();

            if (stacks.Count == 0)
            {
                if (nodeToStack is not WindowNode w0)
                {
                    return originalParent;
                }

                try
                {
                    WrapInStackPanel(w0);
                    ArrangeWithFallback(w0);
                    if (!CanFitLossy(w0.Parent!, window))
                    {
                        w0.Parent!.CollapseIfSingle();
                        return originalParent;
                    }
                }
                catch (TilingFailedException)
                {
                    return originalParent;
                }

                return w0.Parent!;
            }

            // Up to maxStackPanels (same cap as AutoSplitCount): turn other unstacked tiles into stacks so new windows can spread out.
            if (stacks.Count < maxStackPanels)
            {
                var unstacked = CollectUnstackedWindows(originalParent).ToList();
                var preferred = nodeToStack is WindowNode avoidWrap
                    ? unstacked.FirstOrDefault(w => w != avoidWrap)
                    : null;
                var candidate = preferred ?? unstacked.FirstOrDefault();
                if (candidate != null)
                {
                    try
                    {
                        WrapInStackPanel(candidate);
                        ArrangeWithFallback(candidate);
                        stacks = CollectStackPanels(state).ToList();
                    }
                    catch (TilingFailedException)
                    {
                        // Keep existing stacks only
                    }
                }
            }

            stacks = CollectStackPanels(state).ToList();
            var best = stacks
                .OrderBy(CountWindowsInStack)
                .ThenBy(s => s.GenerationID)
                .FirstOrDefault();
            return best ?? originalParent;
        }

        private static int CountWindowsInStack(StackPanelNode stack)
            => stack.Children.Count(static c => c is WindowNode);

        private static IEnumerable<StackPanelNode> CollectStackPanels(DesktopState state)
        {
            var root = state.DesktopTree.Root;
            if (root == null)
            {
                yield break;
            }

            // Walk children only: root.Nodes flattens the full tree, so each stack would be counted many times.
            foreach (var child in root.Children)
            {
                foreach (var sp in EnumerateStackPanelsDeep(child))
                {
                    yield return sp;
                }
            }
        }

        private static IEnumerable<StackPanelNode> EnumerateStackPanelsDeep(TilingNode node)
        {
            if (node is StackPanelNode sp)
            {
                yield return sp;
                yield break;
            }

            if (node is PanelNode panel)
            {
                foreach (var child in panel.Children)
                {
                    foreach (var nested in EnumerateStackPanelsDeep(child))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static IEnumerable<WindowNode> CollectUnstackedWindows(PanelNode parent)
        {
            foreach (var child in parent.Children)
            {
                foreach (var w in EnumerateUnstackedWindowsDeep(child))
                {
                    yield return w;
                }
            }
        }

        private static IEnumerable<WindowNode> EnumerateUnstackedWindowsDeep(TilingNode node)
        {
            switch (node)
            {
                case WindowNode w when !w.PathToRoot.OfType<StackPanelNode>().Any():
                    yield return w;
                    yield break;
                case PanelNode panel:
                    foreach (var child in panel.Children)
                    {
                        foreach (var nested in EnumerateUnstackedWindowsDeep(child))
                        {
                            yield return nested;
                        }
                    }

                    break;
            }
        }

        private static void ArrangeWithFallback(TilingNode node)
        {
            try
            {
                node.Desktop!.Arrange();
            }
            catch (UnsatisfiableFlexConstraintsException)
            {
                node.Parent!.CollapseIfSingle();
            }
        }

        private void AttachToParent(WindowNode newNode, PanelNode parent)
        {
            if (parent is not StackPanelNode && !CanFitLossy(parent, newNode))
                throw new NoValidPlacementExistsException();

            parent.Attach(newNode);
            parent.RemovePlaceholders();
        }

        private static bool CanFitLossy(PanelNode parent, IWindow window)
        {
            if (parent.ComputedRectangle == default)
            {
                return true;
            }

            var node = new WindowNode(window);
            node.Measure();
            return CanFitLossy(parent, node);
        }

        private static bool CanFitLossy(PanelNode parent, TilingNode node)
        {
            if (parent.ComputedRectangle == default)
            {
                return true;
            }

            var minSize = node.MinSize;
            var maxSize = parent.GetMaxSizeForInsert(node);

            return minSize.X <= maxSize.X && minSize.Y <= maxSize.Y;
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

        public TilingNode? NodeAtPoint(IVirtualDesktop currentDesktop, Point pt, IReadOnlySet<IWindow>? excludeWindows = null)
        {
            return WindowAtPointForDrag(currentDesktop, pt, excludeWindows, draggedWindow: null);
        }

        /// <summary>
        /// Window under cursor for hit-testing during drag; <paramref name="draggedWindow"/> refines
        /// focus tie-breaks in <see cref="PickBestHitWindow"/> (same logic as <see cref="MoveNode"/>).
        /// </summary>
        public WindowNode? WindowAtPointForDrag(IVirtualDesktop currentDesktop, Point pt, IReadOnlySet<IWindow>? excludeWindows, IWindow? draggedWindow)
        {
            if (m_states.GetState(currentDesktop) is not DesktopState state)
            {
                throw new ArgumentException("Desktop not registered with backend!");
            }

            WindowNode? draggedNodeHint = draggedWindow != null
                ? state.DesktopTree.FindNode(draggedWindow) as WindowNode
                : null;

            static Rectangle ExpandForDragCueHit(WindowNode node)
            {
                var r = node.ComputedRectangle;
                int expandX = Math.Max(8, r.Width / 40);
                int expandY = Math.Max(8, r.Height / 40);

                // Include panel chrome/padding area so drag hover over tab/header still resolves a target.
                if (node.Parent is PanelNode panel)
                {
                    expandX = Math.Max(expandX, Math.Max(panel.Padding.Left, panel.Padding.Right));
                    expandY = Math.Max(expandY, panel.Padding.Top);
                }

                return Rectangle.OffsetAndSize(
                    r.Left - expandX,
                    r.Top - expandY,
                    r.Width + expandX * 2,
                    r.Height + expandY * 2);
            }

            var hits = state.DesktopTree.Root!.Windows
                .Where(x => excludeWindows == null
                    || x is not WindowNode wn
                    || !excludeWindows.Contains(wn.WindowReference))
                .OfType<WindowNode>()
                .Where(x => ExpandForDragCueHit(x).Contains(pt))
                .ToList();

            if (hits.Count == 0)
            {
                return null;
            }

            if (hits.Count == 1)
            {
                return hits[0];
            }

            var focusedHint = state.FocusedNode as WindowNode;
            return PickBestHitWindow(hits, focusedHint, draggedNodeHint);
        }

        /// <summary>
        /// Stack members share the same <see cref="TilingNode.ComputedRectangle"/> (full stack area).
        /// Pick the focused tab when possible; if both stacked and non-stacked windows hit the same point, prefer stacked.
        /// </summary>
        private static WindowNode PickBestHitWindow(IReadOnlyList<WindowNode> candidates, WindowNode? focusedHint, WindowNode? draggedNode)
        {
            if (candidates.Count == 0)
            {
                throw new ArgumentException(null, nameof(candidates));
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            var stackMembers = candidates.Where(static c => c.Parent is StackPanelNode).ToList();
            var narrowed = stackMembers.Count > 0 && stackMembers.Count < candidates.Count
                ? stackMembers
                : candidates;

            if (narrowed.Count == 1)
            {
                return narrowed[0];
            }

            var focusForTarget = focusedHint != null && !ReferenceEquals(focusedHint, draggedNode)
                ? focusedHint
                : null;
            if (focusForTarget != null && narrowed.Contains(focusForTarget))
            {
                return focusForTarget;
            }

            if (narrowed[0].Parent is StackPanelNode sp && narrowed.All(c => ReferenceEquals(c.Parent, sp)))
            {
                return narrowed.OrderByDescending(c => sp.IndexOf(c)).First();
            }

            return narrowed
                .OrderBy(static c => (long)c.ComputedRectangle.Width * c.ComputedRectangle.Height)
                .ThenBy(static c => c.GenerationID)
                .First();
        }

        public void MoveNode(TilingNode node, Point pt, bool allowNesting = true, bool swapOnDrop = false)
        {
            if (node.Parent == null)
                throw new ArgumentException($"Node cannot be a top-level node!", nameof(node));

            if (node.Desktop == null)
                throw new ArgumentException($"Node must be registered with the backend!", nameof(node));

            var root = node.Desktop.Root!;
            var focusedHint = m_states.GetState(node.Desktop)?.FocusedNode as WindowNode;
            var windowHits = root.Windows
                .Where(x => x != node)
                .OfType<WindowNode>()
                .Where(x => x.ComputedRectangle.Contains(pt))
                .ToList();

            TilingNode? nodeAtPoint;
            if (windowHits.Count > 0)
            {
                var draggedWin = node is WindowNode wn ? wn : null;
                nodeAtPoint = PickBestHitWindow(windowHits, focusedHint, draggedWin);
            }
            else
            {
                nodeAtPoint = root.Nodes
                    .Where(x => x.Type == TilingNodeType.Placeholder)
                    .FirstOrDefault(x => x.ComputedRectangle.Contains(pt))
                    ?? root.Nodes
                        .OfType<PanelNode>()
                        .Where(x => x != node)
                        .FirstOrDefault(x => Rectangle.OffsetAndSize(
                            x.ComputedRectangle.Left - x.Padding.Left,
                            x.ComputedRectangle.Top - x.Padding.Top,
                            x.ComputedRectangle.Width + x.Padding.Left + x.Padding.Right,
                            x.ComputedRectangle.Height + x.Padding.Top + x.Padding.Bottom).Contains(pt));
            }
            if (nodeAtPoint == null || nodeAtPoint.Parent == null)
                return;

            m_logger?.Debug(
                "MoveNode: pt={Pt} windowHitCount={Hits} picked={Pick} pickParent={Parent} allowNesting={Nesting}",
                pt,
                windowHits.Count,
                nodeAtPoint is WindowNode pickedWin ? pickedWin.WindowReference.DebugString() : nodeAtPoint.Type.ToString(),
                nodeAtPoint.Parent!.GetType().Name,
                allowNesting);

            if (nodeAtPoint.PathToRoot.Contains(node))
                throw new TilingFailedException(TilingError.CausesRecursiveNesting);

            if (nodeAtPoint.PathToRoot.OfType<StackPanelNode>().Any() && node is not WindowNode)
                throw new TilingFailedException(TilingError.NestingInStackPanel);

            if (nodeAtPoint.Type == TilingNodeType.Placeholder)
            {
                var oldParent = node.Parent!;
                oldParent.Detach(node);
                nodeAtPoint.Parent!.Attach(node);
                nodeAtPoint.Parent!.RemovePlaceholders();
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
                            if (!MoveNodeTest(node, nodeAtPoint.Parent!, pt))
                            {
                                return;
                            }
                            var dropZone = ClassifyDropZone(nodeAtPoint.ComputedRectangle, pt);
                            m_logger?.Debug(
                                "MoveNode cross-parent: zone={Zone} targetParentBeforeDrop={Parent}",
                                dropZone,
                                nodeAtPoint.Parent!.GetType().Name);
                            EnsureDropZoneParent(nodeAtPoint, dropZone, allowFlipInPlace: false);
                            if (dropZone is DropZone.Left or DropZone.Right or DropZone.Top or DropZone.Bottom
                                && nodeAtPoint is WindowNode edgeTarget
                                && edgeTarget.Parent is SplitPanelNode edgeParent)
                            {
                                var shouldBeVertical = dropZone is DropZone.Top or DropZone.Bottom;
                                var desiredOrientation = shouldBeVertical ? PanelOrientation.Vertical : PanelOrientation.Horizontal;
                                if (edgeParent.Orientation == desiredOrientation && edgeParent.Children.Count >= 2)
                                {
                                    // Cross-parent edge drop should group source+target into a dedicated panel,
                                    // not just append/reorder among existing siblings.
                                    WrapInSplitPanel(edgeTarget, vertical: shouldBeVertical);
                                }
                            }
                            node.Parent!.Detach(node);
                            var insertionIndex = FindInsertionIndex(nodeAtPoint, pt, dropZone);
                            var targetParent = nodeAtPoint.Parent!;
                            targetParent.Attach(ClampInsertionIndex(targetParent, insertionIndex), node);
                            CleanupAfterMove(oldParent);
                            node.Parent.RemovePlaceholders();
                        }
                        catch (UnsatisfiableFlexConstraintsException)
                        {
                            throw new TilingFailedException(TilingError.TargetCannotFit);
                        }
                    }
                    else if (node.Parent is not StackPanelNode)
                    {
                        try
                        {
                            if (allowNesting && node is WindowNode && nodeAtPoint is WindowNode wAtPoint)
                            {
                                var siblingDropZone = ClassifyDropZone(wAtPoint.ComputedRectangle, pt);
                                // Same-parent outcome depends on SiblingDrag mode:
                                //   - center always stacks
                                //   - edges split for everything except Hybrid (which reorders flatly).
                                //     "anything but Hybrid -> split" makes create-panels the default and
                                //     keeps a stale/removed saved value behaving as panel creation.
                                bool splitOnEdge = SiblingDrag != SiblingDragMode.Hybrid;

                                if (siblingDropZone == DropZone.Center)
                                {
                                    EnsureDropZoneParent(wAtPoint, DropZone.Center, allowFlipInPlace: true);
                                    var oldParent = node.Parent!;
                                    oldParent.Detach(node);
                                    var stackParent = (StackPanelNode)wAtPoint.Parent!;
                                    stackParent.Attach(stackParent.Children.Count, node);
                                    CleanupAfterMove(oldParent);
                                    stackParent.RemovePlaceholders();
                                }
                                else if (splitOnEdge)
                                {
                                    // EdgeSplit: edge/corner zones need EnsureDropZoneParent + insert (like cross-parent),
                                    // not only Move() reorder — otherwise left/right/top/bottom never create splits.
                                    // Require pt on the target tile (parent rect can exclude narrow bands at chrome/gaps).
                                    if (!IsPointInWindowDropTarget(wAtPoint, pt))
                                    {
                                        return;
                                    }

                                    m_logger?.Debug(
                                        "MoveNode same-parent split: zone={Zone} parent={Parent}",
                                        siblingDropZone,
                                        node.Parent!.GetType().Name);
                                    var oldParent = node.Parent!;
                                    EnsureDropZoneParent(wAtPoint, siblingDropZone, allowFlipInPlace: true);
                                    // Must compute before Detach: removing a sibling shifts the target's IndexOf.
                                    var insertionIndex = FindInsertionIndex(wAtPoint, pt, siblingDropZone);
                                    oldParent.Detach(node);
                                    var targetParent = wAtPoint.Parent!;
                                    targetParent.Attach(ClampInsertionIndex(targetParent, insertionIndex), node);
                                    CleanupAfterMove(oldParent);
                                    node.Parent!.RemovePlaceholders();
                                }
                                else
                                {
                                    // Hybrid edge zones: reorder within the shared panel without
                                    // nesting, matching legacy flat Move() semantics.
                                    FlatSiblingReorder(node, wAtPoint);
                                }
                            }
                            else
                            {
                                var newPosition = TransferSize(node.ComputedRectangle, nodeAtPoint.ComputedRectangle);
                                if (newPosition.Contains(pt))
                                {
                                    // Node moved over another node that IS a sibling (non-window nodes)
                                    var nodeIndex = node.Parent.Children.IndexOf(node);
                                    var targetIndex = node.Parent.Children.IndexOf(nodeAtPoint);

                                    node.Parent.Move(nodeIndex, targetIndex);
                                }
                            }
                        }
                        catch (UnsatisfiableFlexConstraintsException)
                        {
                            throw new TilingFailedException(TilingError.Failed);
                        }
                    }
                    else if (node.Parent is StackPanelNode stackSiblingParent)
                    {
                        try
                        {
                            if (allowNesting && node is WindowNode && nodeAtPoint is WindowNode wAtPoint2)
                            {
                                var siblingDropZone = ClassifyDropZone(wAtPoint2.ComputedRectangle, pt);
                                if (siblingDropZone == DropZone.Center)
                                {
                                    EnsureDropZoneParent(wAtPoint2, DropZone.Center, allowFlipInPlace: true);
                                    var targetStack = (StackPanelNode)wAtPoint2.Parent!;
                                    var oldParent = node.Parent!;
                                    oldParent.Detach(node);
                                    targetStack.Attach(targetStack.Children.Count, node);
                                    if (!ReferenceEquals(oldParent, targetStack))
                                    {
                                        CleanupAfterMove(oldParent);
                                    }
                                    else
                                    {
                                        targetStack.RemovePlaceholders();
                                    }
                                }
                                else
                                {
                                    // Same stack: non-center zones use ClassifyDropZone + FindInsertionIndex (see same-parent split above).
                                    if (!IsPointInWindowDropTarget(wAtPoint2, pt))
                                    {
                                        return;
                                    }

                                    var oldParent = node.Parent!;
                                    EnsureDropZoneParent(wAtPoint2, siblingDropZone, allowFlipInPlace: true);
                                    var insertionIndex = FindInsertionIndex(wAtPoint2, pt, siblingDropZone);
                                    oldParent.Detach(node);
                                    var targetParent = wAtPoint2.Parent!;
                                    targetParent.Attach(ClampInsertionIndex(targetParent, insertionIndex), node);
                                    CleanupAfterMove(oldParent);
                                    node.Parent!.RemovePlaceholders();
                                }
                            }
                        }
                        catch (UnsatisfiableFlexConstraintsException)
                        {
                            throw new TilingFailedException(TilingError.Failed);
                        }
                    }
                }
                // Swap behavior is opt-in (Shift modifier).
                // Do not tie this to allowNesting=false, because that flag is also used by
                // the "disable drag-drop auto panel creation" setting.
                else if (swapOnDrop && node.Parent != nodeAtPoint)
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
                else
                {
                    try
                    {
                        // Non-nesting fallback path:
                        // preserve intuitive drag behavior (move/reorder) without creating new
                        // panel structures and without swapping source/target windows.
                        var oldParent = node.Parent!;
                        var targetParent = nodeAtPoint.Parent!;
                        var dropZone = ClassifyDropZone(nodeAtPoint.ComputedRectangle, pt);
                        var insertionIndex = FindInsertionIndex(nodeAtPoint, pt, dropZone);

                        if (!ReferenceEquals(oldParent, targetParent))
                        {
                            if (!MoveNodeTest(node, targetParent, pt))
                            {
                                return;
                            }

                            oldParent.Detach(node);
                            targetParent.Attach(ClampInsertionIndex(targetParent, insertionIndex), node);
                            CleanupAfterMove(oldParent);
                            node.Parent!.RemovePlaceholders();
                        }
                        else
                        {
                            // Same-parent, non-nesting: flat reorder into the target's slot.
                            // Legacy Move(source, IndexOf(target)) semantics — the insertion-index
                            // path no-ops when the cursor sits on the target's exact midpoint.
                            FlatSiblingReorder(node, nodeAtPoint);
                        }
                    }
                    catch (UnsatisfiableFlexConstraintsException)
                    {
                        throw new TilingFailedException(TilingError.Failed);
                    }
                }
            }
        }

        /// <summary>
        /// Reorders <paramref name="node"/> into <paramref name="targetSibling"/>'s slot within their
        /// shared parent, shifting the rest. Reproduces the pre-drop-zone flat Move() reorder that the
        /// flat-panel tests assert. <see cref="PanelNode.Move"/> inserts at the target index in the
        /// post-removal list, so passing the target's pre-removal index lands the node where expected.
        /// </summary>
        private static void FlatSiblingReorder(TilingNode node, TilingNode targetSibling)
        {
            var parent = node.Parent;
            if (parent == null || !ReferenceEquals(parent, targetSibling.Parent))
            {
                return;
            }

            var sourceIndex = parent.IndexOf(node);
            var targetIndex = parent.IndexOf(targetSibling);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            {
                return;
            }

            parent.Move(sourceIndex, targetIndex);
        }

        private static int FindInsertionIndex(TilingNode nodeAtPoint, Point pt, DropZone dropZone)
        {
            Debug.Assert(nodeAtPoint.Parent != null);
            var insertionIndex = nodeAtPoint.Parent!.IndexOf(nodeAtPoint);
            if (dropZone == DropZone.Center && nodeAtPoint.Parent is StackPanelNode stackParent)
            {
                return stackParent.Children.Count;
            }

            if (dropZone == DropZone.Neutral)
            {
                var r = nodeAtPoint.ComputedRectangle;
                var midX = r.Left + r.Width / 2;
                var midY = r.Top + r.Height / 2;
                var dx = pt.X - midX;
                var dy = pt.Y - midY;
                if (Math.Abs(dx) >= Math.Abs(dy))
                {
                    return dx < 0 ? insertionIndex : insertionIndex + 1;
                }

                return dy < 0 ? insertionIndex : insertionIndex + 1;
            }

            if (dropZone == DropZone.Left || dropZone == DropZone.Top)
            {
                return insertionIndex;
            }

            if (dropZone == DropZone.Right || dropZone == DropZone.Bottom)
            {
                return insertionIndex + 1;
            }

            if (nodeAtPoint.Parent is GridLikeNode grid)
            {
                if (grid.CanResizeInOrientation(PanelOrientation.Horizontal))
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

        private static int ClampInsertionIndex(PanelNode parent, int insertionIndex)
        {
            if (insertionIndex < 0)
            {
                return 0;
            }

            if (insertionIndex > parent.Children.Count)
            {
                return parent.Children.Count;
            }

            return insertionIndex;
        }

        private static bool IsPointInWindowDropTarget(WindowNode window, Point pt)
        {
            var r = window.ComputedRectangle;
            int expandX = Math.Max(8, r.Width / 40);
            int expandY = Math.Max(24, r.Height / 12);
            if (window.Parent is PanelNode panel)
            {
                expandX = Math.Max(panel.Padding.Left, panel.Padding.Right);
                expandY = Math.Max(expandY, panel.Padding.Top);
            }

            var target = Rectangle.OffsetAndSize(
                r.Left - expandX,
                r.Top - expandY,
                r.Width + expandX * 2,
                r.Height + expandY * 2);
            return target.Contains(pt);
        }

        private static bool IsRedundantSplitWithSingleStackChild(PanelNode panel)
        {
            return panel is SplitPanelNode
                && panel.Children.Count == 1
                && panel.Children[0] is StackPanelNode;
        }

        private void CleanupAfterMove(PanelNode oldParent)
        {
            oldParent.Cleanup(collapse: AutoCollapse);
            if (IsRedundantSplitWithSingleStackChild(oldParent))
            {
                oldParent.Cleanup(collapse: true);
            }
        }

        /// <summary>
        /// Two sibling windows only (no nested panels) — changing orientation in place avoids redundant nesting.
        /// </summary>
        private static bool IsReplaceableSimpleTwoWindowSplit(SplitPanelNode splitParent)
        {
            return splitParent.Children.Count == 2
                && splitParent.Children[0] is WindowNode
                && splitParent.Children[1] is WindowNode;
        }

        private static bool IsSimpleTwoWindowSplitSubtree(SplitPanelNode splitRoot)
        {
            static (bool valid, int windowCount) Walk(TilingNode node)
            {
                if (node is WindowNode)
                {
                    return (true, 1);
                }

                if (node is PlaceholderNode or StackPanelNode)
                {
                    return (false, 0);
                }

                if (node is not SplitPanelNode split || split.Children.Count != 2)
                {
                    return (false, 0);
                }

                var left = Walk(split.Children[0]);
                if (!left.valid)
                {
                    return (false, 0);
                }

                var right = Walk(split.Children[1]);
                if (!right.valid)
                {
                    return (false, 0);
                }

                return (true, left.windowCount + right.windowCount);
            }

            var (valid, windowCount) = Walk(splitRoot);
            return valid && windowCount == 2;
        }

        private bool TryFlipSimpleTwoWindowSplitAncestor(WindowNode nodeAtPoint, PanelOrientation desiredOrientation)
        {
            SplitPanelNode? candidate = null;
            for (var cursor = nodeAtPoint.Parent; cursor is SplitPanelNode split; cursor = split.Parent)
            {
                if (IsSimpleTwoWindowSplitSubtree(split))
                {
                    candidate = split;
                }
            }

            if (candidate == null)
            {
                return false;
            }

            if (candidate.Orientation != desiredOrientation)
            {
                m_logger?.Debug(
                    "EnsureDropZone: flip simple split ancestor in place ({From} -> {To}, Gen={Gen})",
                    candidate.Orientation,
                    desiredOrientation,
                    candidate.GenerationID);
                candidate.Orientation = desiredOrientation;
            }

            return true;
        }

        private void EnsureDropZoneParent(TilingNode nodeAtPoint, DropZone dropZone, bool allowFlipInPlace)
        {
            if (nodeAtPoint is not WindowNode)
            {
                return;
            }

            if (dropZone == DropZone.Neutral)
            {
                return;
            }

            if (dropZone == DropZone.Center)
            {
                if (nodeAtPoint.Parent is not StackPanelNode)
                {
                    m_logger?.Debug(
                        "EnsureDropZone: center drop wraps window in new stack (was parent {Parent})",
                        nodeAtPoint.Parent?.GetType().Name);
                    WrapInStackPanel(nodeAtPoint);
                }
                else
                {
                    m_logger?.Debug("EnsureDropZone: center drop joins existing stack (parent Gen {Gen})", nodeAtPoint.Parent!.GenerationID);
                }

                return;
            }

            // Edge drops on windows already in a stack: insert/reorder via StackPanel + FindInsertionIndex.
            // Wrapping here would call WrapInSplitPanel on a stacked window and throw NestingInStackPanel.
            if (nodeAtPoint.Parent is StackPanelNode)
            {
                return;
            }

            if (nodeAtPoint.Parent is not SplitPanelNode splitParent)
            {
                WrapInSplitPanel(nodeAtPoint, vertical: dropZone is DropZone.Top or DropZone.Bottom);
                return;
            }

            var shouldBeVertical = dropZone is DropZone.Top or DropZone.Bottom;
            var desiredOrientation = shouldBeVertical ? PanelOrientation.Vertical : PanelOrientation.Horizontal;
            if (splitParent.Orientation != desiredOrientation)
            {
                if (allowFlipInPlace && IsReplaceableSimpleTwoWindowSplit(splitParent))
                {
                    m_logger?.Debug(
                        "EnsureDropZone: flip split orientation in place (two windows, {From} -> {To})",
                        splitParent.Orientation,
                        desiredOrientation);
                    splitParent.Orientation = desiredOrientation;
                }
                else if (allowFlipInPlace && TryFlipSimpleTwoWindowSplitAncestor((WindowNode)nodeAtPoint, desiredOrientation))
                {
                    // Simple two-window split chains are orientation-equivalent; avoid creating
                    // another nested split wrapper when we can reuse an existing ancestor split.
                }
                else
                {
                    WrapInSplitPanel(nodeAtPoint, vertical: shouldBeVertical);
                }
            }
            else if (splitParent.Children.Count > 2)
            {
                // In larger same-orientation splits, edge drop should create a focused sub-panel
                // around the target window instead of only reordering siblings.
                WrapInSplitPanel(nodeAtPoint, vertical: shouldBeVertical);
            }
        }

        internal static DropZone ClassifyDropZone(Rectangle targetRect, Point pt)
        {
            if (targetRect.Width <= 0 || targetRect.Height <= 0)
            {
                return DropZone.Center;
            }

            var corner = (int)(Math.Min(targetRect.Width, targetRect.Height) * DropZoneCornerFraction);
            if (corner > 0)
            {
                var inNw = pt.X < targetRect.Left + corner && pt.Y < targetRect.Top + corner;
                var inNe = pt.X > targetRect.Right - corner && pt.Y < targetRect.Top + corner;
                var inSw = pt.X < targetRect.Left + corner && pt.Y > targetRect.Bottom - corner;
                var inSe = pt.X > targetRect.Right - corner && pt.Y > targetRect.Bottom - corner;
                if (inNw || inNe || inSw || inSe)
                {
                    return DropZone.Neutral;
                }
            }

            var centerLeft = targetRect.Left + (int)(targetRect.Width * 0.30);
            var centerRight = targetRect.Right - (int)(targetRect.Width * 0.30);
            var centerTop = targetRect.Top + (int)(targetRect.Height * 0.30);
            var centerBottom = targetRect.Bottom - (int)(targetRect.Height * 0.30);
            if (pt.X >= centerLeft && pt.X <= centerRight && pt.Y >= centerTop && pt.Y <= centerBottom)
            {
                return DropZone.Center;
            }

            var midX = targetRect.Left + targetRect.Width / 2;
            var midY = targetRect.Top + targetRect.Height / 2;
            var dx = pt.X - midX;
            var dy = pt.Y - midY;
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return dx < 0 ? DropZone.Left : DropZone.Right;
            }

            return dy < 0 ? DropZone.Top : DropZone.Bottom;
        }

        private static Rectangle Inset(Rectangle r, int margin)
        {
            if (r.Width <= margin * 2 || r.Height <= margin * 2)
            {
                return r;
            }

            return Rectangle.OffsetAndSize(
                r.Left + margin,
                r.Top + margin,
                r.Width - margin * 2,
                r.Height - margin * 2);
        }

        /// <summary>
        /// Preview regions representing resulting layout after drop.
        /// Left/Right/Top/Bottom render split outcome panes; Center renders stack glow area.
        /// </summary>
        internal static void GetDropZoneHighlightRects(
            Rectangle targetRect,
            DropZone activeZone,
            out Rectangle center,
            out Rectangle left,
            out Rectangle top,
            out Rectangle right,
            out Rectangle bottom)
        {
            center = default;
            left = default;
            top = default;
            right = default;
            bottom = default;
            if (targetRect.Width <= 0 || targetRect.Height <= 0)
            {
                return;
            }

            var inset = Inset(targetRect, margin: 8);
            var midX = inset.Left + inset.Width / 2;
            var midY = inset.Top + inset.Height / 2;

            switch (activeZone)
            {
                case DropZone.Center:
                    center = inset;
                    break;
                case DropZone.Left:
                    left = Inset(Rectangle.OffsetAndSize(inset.Left, inset.Top, midX - inset.Left, inset.Height), 4);
                    right = Inset(Rectangle.OffsetAndSize(midX, inset.Top, inset.Right - midX, inset.Height), 4);
                    break;
                case DropZone.Right:
                    left = Inset(Rectangle.OffsetAndSize(inset.Left, inset.Top, midX - inset.Left, inset.Height), 4);
                    right = Inset(Rectangle.OffsetAndSize(midX, inset.Top, inset.Right - midX, inset.Height), 4);
                    break;
                case DropZone.Top:
                    top = Inset(Rectangle.OffsetAndSize(inset.Left, inset.Top, inset.Width, midY - inset.Top), 4);
                    bottom = Inset(Rectangle.OffsetAndSize(inset.Left, midY, inset.Width, inset.Bottom - midY), 4);
                    break;
                case DropZone.Bottom:
                    top = Inset(Rectangle.OffsetAndSize(inset.Left, inset.Top, inset.Width, midY - inset.Top), 4);
                    bottom = Inset(Rectangle.OffsetAndSize(inset.Left, midY, inset.Width, inset.Bottom - midY), 4);
                    break;
                case DropZone.Neutral:
                    // Keep a subtle cue visible in corner/neutral regions too.
                    center = Inset(inset, 6);
                    break;
            }
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

            var nodeAtPointClone = rootClone.Windows
                .Where(x => x != nodeClone)
                .OfType<WindowNode>()
                .FirstOrDefault(x => IsPointInWindowDropTarget(x, pt));
            if (nodeAtPointClone != null)
            {
                var dropZone = ClassifyDropZone(nodeAtPointClone.ComputedRectangle, pt);
                var insertionIndex = FindInsertionIndex(nodeAtPointClone, pt, dropZone);
                newParentClone.Attach(ClampInsertionIndex(newParentClone, insertionIndex), nodeClone);
            }
            else
            {
                newParentClone.Attach(nodeClone);
            }
            newParentClone.RemovePlaceholders();
            testTree.Measure();
            testTree.Arrange();

            var targetRect = Rectangle.OffsetAndSize(
                newParentClone.ComputedRectangle.Left - newParentClone.Padding.Left,
                newParentClone.ComputedRectangle.Top - newParentClone.Padding.Top,
                newParentClone.ComputedRectangle.Width + newParentClone.Padding.Left + newParentClone.Padding.Right,
                newParentClone.ComputedRectangle.Height + newParentClone.Padding.Top + newParentClone.Padding.Bottom);
            return targetRect.Contains(pt);
        }

        private static Rectangle TransferSize(Rectangle a, Rectangle b)
        {
            var newCenter = b.Center;
            var width = a.Width;
            var height = a.Height;

            return new Rectangle(newCenter.X - width / 2, newCenter.Y - height / 2, newCenter.X + width / 2, newCenter.Y + height / 2);
        }

        public void MoveWindow(IWindow window, Point pt, bool allowNesting, bool swapOnDrop = false)
        {
            var state = m_states.FindByTree(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            var sourceNode = state.DesktopTree.FindNode(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            MoveNode(sourceNode, pt, allowNesting, swapOnDrop);
        }


        public (Rectangle preArrange, Rectangle postArrange) MockMoveWindow(IWindow window, Point pt, bool allowNesting, bool swapOnDrop = false)
        {
            var state = m_states.FindByTree(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            var sourceNode = state.DesktopTree.FindNode(window) ?? throw new ArgumentException($"Window must be registered with the backend!", nameof(window));
            return MockMoveNode(sourceNode, pt, allowNesting, swapOnDrop);
        }

        public (Rectangle preArrange, Rectangle postArrange) MockMoveNode(TilingNode sourceNode, Point pt, bool allowNesting, bool swapOnDrop = false)
        {
            var desktop = sourceNode.Desktop!;
            var rootClone = (PanelNode)desktop.Root!.Clone();

            var sourceNodeClone = rootClone.Nodes.First(x => x.GenerationID == sourceNode.GenerationID);
            var testTree = new DesktopTree
            {
                Root = rootClone,
                WorkArea = desktop.WorkArea,
            };

            MoveNode(sourceNodeClone, pt, allowNesting, swapOnDrop);

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
                // unconstrainedParentClone was cloned before Arrange(); its ComputedRectangle is stale.
                // Use the arranged parent's bounds from the test tree (required for Flex.SetContainerWidth >= 1).
                var parentBounds = new RectangleF(sourceNodeClone.Parent!.ComputedRectangle);
                if (parentBounds.Width < 1 || parentBounds.Height < 1)
                {
                    throw new TilingFailedException(TilingError.NoValidPlacementExists);
                }

                unconstrainedParentClone.Arrange(parentBounds);
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
                ResizeNode(node, newPosition, oldPosition);
            }
        }

        public void ResizeNode(TilingNode node, Rectangle newPosition, Rectangle oldPosition)
        {
            if (newPosition.Width != oldPosition.Width)
            {
                GridLikeNode? p = node.Ancestors
                    .Select(x => x as GridLikeNode)
                    .Where(x => x != null)
                    .FirstOrDefault(x => x!.CanResizeInOrientation(PanelOrientation.Horizontal));

                if (p != null)
                {
                    var leftResizeAmount = Math.Abs(newPosition.Left - oldPosition.Left);
                    var rightResizeAmount = Math.Abs(newPosition.Right - oldPosition.Right);
                    GrowDirection direction = leftResizeAmount < rightResizeAmount
                        ? GrowDirection.TowardsEnd
                        : leftResizeAmount > rightResizeAmount
                            ? GrowDirection.TowardsStart
                            : GrowDirection.Both;
                    var child = p.Children.First(x => x.Nodes.Contains(node));
                    var childIndex = p.IndexOf(child);
                    var sizeDelta = newPosition.Width - oldPosition.Width;

                    if (direction == GrowDirection.TowardsStart && childIndex == 0 && child.GetAdjacentNode(TilingDirection.Left) is TilingNode leftNode)
                    {
                        var leftNodePosition = leftNode.ComputedRectangle;
                        ResizeNode(leftNode, new Rectangle(leftNodePosition.Left, leftNodePosition.Top, leftNodePosition.Right - sizeDelta, leftNodePosition.Bottom), leftNodePosition);
                    }
                    else if (direction == GrowDirection.TowardsEnd && childIndex == p.Children.Count - 1 && child.GetAdjacentNode(TilingDirection.Right) is TilingNode rightNode)
                    {
                        var rightNodePosition = rightNode.ComputedRectangle;
                        ResizeNode(rightNode, new Rectangle(rightNodePosition.Left + sizeDelta, rightNodePosition.Top, rightNodePosition.Right, rightNodePosition.Bottom), rightNodePosition);
                    }

                    p.ResizeBy(child, sizeDelta, direction);
                }
            }

            if (newPosition.Height != oldPosition.Height)
            {
                GridLikeNode? p = node.Ancestors
                    .Select(x => x as GridLikeNode)
                    .Where(x => x != null)
                    .FirstOrDefault(x => x!.CanResizeInOrientation(PanelOrientation.Vertical));

                if (p != null)
                {
                    var topResizeAmount = Math.Abs(newPosition.Top - oldPosition.Top);
                    var bottomResizeAmount = Math.Abs(newPosition.Bottom - oldPosition.Bottom);
                    GrowDirection direction = topResizeAmount < bottomResizeAmount
                        ? GrowDirection.TowardsEnd
                        : topResizeAmount > bottomResizeAmount
                            ? GrowDirection.TowardsStart
                            : GrowDirection.Both;
                    var child = p.Children.First(x => x.Nodes.Contains(node));
                    var childIndex = p.IndexOf(child);
                    var sizeDelta = newPosition.Height - oldPosition.Height;

                    if (direction == GrowDirection.TowardsStart && childIndex == 0 && child.GetAdjacentNode(TilingDirection.Up) is TilingNode topNode)
                    {
                        var topNodePosition = topNode.ComputedRectangle;
                        ResizeNode(topNode, new Rectangle(topNodePosition.Left, topNodePosition.Top, topNodePosition.Right, topNodePosition.Bottom - sizeDelta), topNodePosition);
                    }
                    else if (direction == GrowDirection.TowardsEnd && childIndex == p.Children.Count - 1 && child.GetAdjacentNode(TilingDirection.Down) is TilingNode bottomNode)
                    {
                        var bottomNodePosition = bottomNode.ComputedRectangle;
                        ResizeNode(bottomNode, new Rectangle(bottomNodePosition.Left, bottomNodePosition.Top + sizeDelta, bottomNodePosition.Right, bottomNodePosition.Bottom), bottomNodePosition);
                    }

                    p.ResizeBy(child, sizeDelta, direction);
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
            Debug.Assert(node.Parent != null);
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
                return CanFitLossy(parent, child);
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
