using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

using FancyWM.Utilities;

using WinMan;
using FancyWM.Layouts.Tiling;
using FancyWM.Layouts;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Linq;
using System.Diagnostics;

namespace FancyWM
{
    internal partial class TilingService
    {
        private void RestoreOriginalLayout()
        {
            using (m_backendLock.EnterScope())
            {
                foreach (var desktop in m_workspace.VirtualDesktopManager.Desktops)
                {
                    try
                    {
                        var tree = m_backend.GetTree(desktop);
                        if (tree == null)
                            continue;

                        foreach (var window in tree.Root!.Windows)
                        {
                            var originalPosition = m_backend.GetOriginalPosition(window.WindowReference);
                            try
                            {
                                window.WindowReference.SetPosition(originalPosition);
                            }
                            catch (InvalidWindowReferenceException)
                            {
                                continue;
                            }
                            catch (InvalidOperationException) when (window.WindowReference.State != WindowState.Restored)
                            {
                                continue;
                            }
                        }
                    }
                    catch (KeyNotFoundException)
                    {
                        continue;
                    }
                    catch (InvalidOperationException e)
                    {
                        m_logger.Warning(e, "Exception thrown while restoring the original window layout!");
                    }
                }
            }
        }

        private TimeSpan m_lastUpdateLayout = TimeSpan.Zero;

        private void SyncPanelChromeMetrics(DesktopTree tree)
        {
            var pad = GetPanelPaddingRect();
            var spacing = GetPanelSpacing();
            foreach (var panel in tree.Root!.Nodes.OfType<PanelNode>())
            {
                panel.Padding = pad;
                panel.Spacing = spacing;
            }
        }

        private void UpdateTree(DesktopTree tree)
        {
            tree.WorkArea = m_display.WorkArea;
            SyncPanelChromeMetrics(tree);

            bool constraintsSatisfied = false;
            while (!constraintsSatisfied)
            {
                tree.Measure();
                try
                {
                    tree.Arrange();
                    constraintsSatisfied = true;
                }
                catch (UnsatisfiableFlexConstraintsException)
                {
                    var largestWindow = tree.Root!.Windows.OrderByDescending(x => x.GenerationID).First();
                    m_logger.Warning($"The arrange pass failed! Floating window {largestWindow.WindowReference.DebugString()} in an attempt to find a permissible arrangement!");
                    using (m_floatingSetLock.EnterScope())
                    {
                        m_floatingSet.Add(largestWindow.WindowReference);
                    }
                    // Track for retry — flex constraints are often transient after
                    // display reconnect / hibernation resume.
                    using (m_placementFailedSetLock.EnterScope())
                    {
                        m_placementFailedSet.Add(largestWindow.WindowReference);
                    }
                    DetectChanges(largestWindow.WindowReference);
                    PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NoValidPlacementExists, largestWindow.WindowReference));
                }
            }
        }

        private async Task UpdateLayoutAsync()
        {
            if (!Active)
                return;

            if (m_currentInteraction != UserInteraction.None && m_sw.Elapsed - m_lastUpdateLayout <= TimeSpan.FromSeconds(1.0 / m_display.RefreshRate))
            {
                return;
            }
            m_lastUpdateLayout = m_sw.Elapsed;

            IVirtualDesktop desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;

            List<TilingNode> snapshot;
            IReadOnlyCollection<TilingNode> focusedPath;
            TilingNode? focusedNode;
            DesktopTree tree;

            using (m_backendLock.EnterScope())
            {
                try
                {
                    var treeOrNull = m_backend.GetTree(desktop);
                    if (treeOrNull == null)
                        return;
                    tree = treeOrNull;
                }
                catch (KeyNotFoundException)
                {
                    m_logger.Warning($"Current desktop {desktop} is not registered with backend, aborting...");
                    return;
                }

                UpdateTree(tree);

                snapshot = tree.Root!.Nodes.Skip(1).ToList();
                focusedNode = m_backend.GetFocus(desktop);
                focusedPath = (IReadOnlyCollection<TilingNode>?)focusedNode?.PathToRoot?.ToList() ?? [];
            }

            async ValueTask RepositionAsync()
            {
                try
                {
                    Freeze();
                    IList<WindowNode> snapshotWindows;
                    using (m_ignoreRepositionSetLock.EnterScope())
                    {
                        snapshotWindows = snapshot.OfType<WindowNode>().Where(x => !m_ignoreRepositionSet.Contains(x.WindowReference)).ToList();
                    }

                    bool useSmoothing = m_animateWindowMovement && m_currentInteraction != UserInteraction.Resizing;
                    await UpdateWindowPositionsAsync(snapshotWindows, useSmoothing);
                }
                finally
                {
                    Unfreeze();
                }
            }

            m_gui.FocusRectangle = GetFocusRectangle(focusedNode);

            var repositionTask = RepositionAsync();

            m_gui.UpdateOverlay(snapshot, focusedPath);
            m_gui.PreviewRectangle = GetPreviewRectangle();
            m_gui.DropZonePreview = GetDropZonePreviewState();

            if (m_showPreviewFocus)
            {
                // TODO: Can we just use focusedNode here?
                var previewWindows = m_workspace.VirtualDesktopManager.Desktops
                    .Select(desktop => m_backend.GetFocus(desktop))
                    .OfType<WindowNode>()
                    .Select(x => x.WindowReference)
                    .ToHashSet();
                m_gui.PreviewWindows = previewWindows;
            }
            else
            {
                m_gui.PreviewWindows = EmptyWindowSet;
            }

            await repositionTask;
        }

        private async Task UpdateWindowPositionsAsync(IEnumerable<WindowNode> snapshot, bool useSmoothing)
        {
            var targets = CalculateRepositionTargets(snapshot);
            foreach (var target in targets)
            {
                if (target.OriginalPosition != target.ComputedPosition)
                {
                    m_logger.Information("Relocating window {Window} from {OriginalPosition} to {ComputedPosition}",
                        target.Window.DebugString(),
                        target.OriginalPosition, target.ComputedPosition);
                }
                else
                {
                    m_logger.Information("Window {Window} location is {ComputedPosition}",
                        target.Window.DebugString(),
                        target.ComputedPosition);
                }
            }

            HashSet<IWindow>? newWindows = null;
            using (m_newWindowSetLock.EnterScope())
            {
                if (m_newWindowSet.Count > 0)
                {
                    newWindows = [.. m_newWindowSet];
                    m_newWindowSet.Clear();
                }
            }

            if (useSmoothing)
            {
                var focusRectangle = m_gui.FocusRectangle;
                m_gui.FocusRectangle = null;

                TransitionTargetGroup transitionGroup;
                if (newWindows != null)
                {
                    await TransitionTargetGroup.PerformTransitionAsync(targets.Where(x => newWindows!.Contains(x.Window)).ToList());
                    transitionGroup = new TransitionTargetGroup(m_animationThread, targets.Where(x => !newWindows!.Contains(x.Window)));
                }
                else
                {
                    transitionGroup = new TransitionTargetGroup(m_animationThread, targets);
                }
                await transitionGroup.PerformSmoothTransitionAsync(TimeSpan.FromMilliseconds(100));

                m_gui.FocusRectangle = focusRectangle;
            }
            else
            {
                await TransitionTargetGroup.PerformTransitionAsync(targets);
            }
        }

        private List<TransitionTarget> CalculateRepositionTargets(IEnumerable<WindowNode> snapshot)
        {
            var targets = new List<TransitionTarget>();
            foreach (var window in snapshot)
            {
                try
                {
                    var currentPosition = window.WindowReference.Position;
                    if (!window.WindowReference.CanResize)
                    {
                        m_logger.Warning("Unresizable window {Window} will be moved only", window.WindowReference.DebugString());
                        var targetRect = ShrinkTo(window.ComputedRectangle, currentPosition.Width, currentPosition.Height);
                        if (targetRect == currentPosition)
                        {
                            continue;
                        }
                        targets.Add(new TransitionTarget(window.WindowReference, currentPosition, targetRect));
                    }
                    else
                    {
                        m_logger.Debug("Updating position of window {Window}", window.WindowReference.DebugString());
                        var rect = window.ComputedRectangle;
                        var frame = window.WindowReference.FrameMargins;
                        var adjustedRect = new Rectangle(
                            left: rect.Left - frame.Left,
                            top: rect.Top - frame.Top,
                            right: rect.Right + frame.Right,
                            bottom: rect.Bottom + frame.Bottom);

                        if (adjustedRect == currentPosition)
                        {
                            continue;
                        }

                        targets.Add(new TransitionTarget(window.WindowReference, currentPosition, adjustedRect));

                        var minSize = window.WindowReference.MinSize;
                        if (minSize.HasValue)
                        {
                            if (minSize.Value.X > adjustedRect.Width)
                            {
                                m_logger.Warning("New width for {Window} is smaller than the value reported by WM_GETMINMAXINFO ({ComputedWidth} < {MinimumWidth})",
                                    window.WindowReference.DebugString(), adjustedRect.Width, minSize.Value.X);
                            }
                            if (minSize.Value.Y > adjustedRect.Height)
                            {
                                m_logger.Warning("New height for {Window} is smaller than the value reported by WM_GETMINMAXINFO ({ComputedHeight} < {MinimumHeight})",
                                    window.WindowReference.DebugString(), adjustedRect.Height, minSize.Value.Y);
                            }
                        }
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    // Ignore
                }
                catch (Win32Exception e)
                {
                    m_logger.Error(e, "Failed to calculate reposition targets");
                }
            }
            return targets;
        }

        private bool CanShowFocusRectangle()
        {
            return m_showFocus && m_currentInteraction == UserInteraction.None && m_movingPanelNode == null;
        }

        private Rectangle? GetFocusRectangle(TilingNode? focusedNode)
        {
            if (focusedNode is WindowNode focusedWindow && CanShowFocusRectangle())
            {
                return focusedWindow.ComputedRectangle;
            }
            return null;
        }

        private Rectangle? GetPreviewRectangle()
        {
            // WM_NCHITTEST classified this gesture as a border resize, not a move.
            if (m_borderResizeGesture)
                return null;

            // Mouse drag already released: hide the cue immediately rather than waiting
            // for a PositionChangeEnd that some windows never emit (keyboard moves keep theirs).
            if (m_activeDragWindow != null && m_activeDragIsMouse && !m_leftButtonDown)
                return null;

            var windowDragPreview =
                m_currentInteraction == UserInteraction.Moving
                || (m_currentInteraction == UserInteraction.Starting && m_activeDragWindow != null)
                || (m_currentInteraction == UserInteraction.None && m_activeDragWindow != null);
            if (!windowDragPreview && m_movingPanelNode == null)
            {
                return null;
            }

            try
            {
                var isSwapping = IsSwapModifierPressed();
                var pt = m_workspace.CursorLocation;
                // Keep these two controls independent:
                // - allowNesting: enables/disables automatic panel creation
                // - swapOnDrop: explicit swap gesture (Shift)
                // This avoids accidental swap behavior when auto panel creation is disabled.

                if (m_movingPanelNode == null)
                {
                    var window = m_activeDragWindow ?? m_workspace.FocusedWindow;
                    if (window == null)
                    {
                        return null;
                    }

                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.HasWindow(window))
                        {
                            return m_backend.MockMoveWindow(
                                window,
                                pt,
                                allowNesting: m_enableDragDropAutoPanelCreation && !isSwapping,
                                swapOnDrop: isSwapping).preArrange;
                        }
                    }
                }
                else
                {
                    using (m_backendLock.EnterScope())
                    {
                        var rect = m_backend.MockMoveNode(
                            m_movingPanelNode,
                            pt,
                            allowNesting: m_enableDragDropAutoPanelCreation && !isSwapping,
                            swapOnDrop: isSwapping).preArrange;
                        var padding = GetPanelPaddingRect();
                        var spacing = GetPanelSpacing();
                        return new Rectangle(
                            rect.Left - padding.Left - spacing / 2,
                            rect.Top - padding.Top - spacing / 2,
                            rect.Right + padding.Right + spacing / 2,
                            rect.Bottom + padding.Bottom + spacing / 2);
                    }
                }
            }
            catch (TilingFailedException)
            {
            }
            catch (InvalidWindowReferenceException)
            {
            }
            catch (Exception ex)
            {
                m_logger.Warning(ex, "Failed to compute drag preview rectangle");
            }

            return null;
        }

        private HashSet<IWindow>? GetDragExcludeWindows()
        {
            var set = new HashSet<IWindow>();
            if (m_activeDragWindow != null)
            {
                set.Add(m_activeDragWindow);
            }

            if (m_movingPanelNode != null)
            {
                foreach (var w in m_movingPanelNode.Windows)
                {
                    set.Add(w.WindowReference);
                }
            }

            return set.Count > 0 ? set : null;
        }

        private DropZonePreviewState? GetDropZonePreviewState()
        {
            // Suppress cues when WM_NCHITTEST told us this is a border resize,
            // or when the size-changed heuristic flagged it as Resizing.
            if (m_borderResizeGesture || m_currentInteraction == UserInteraction.Resizing)
            {
                return null;
            }

            if (!m_enableDragDropAutoPanelCreation)
            {
                // Drop-zone preview communicates panel-creation outcomes; hide it when
                // auto-creation is disabled to keep visual intent aligned with behavior.
                return null;
            }

            // Mouse drag already released: hide cues immediately (see GetPreviewRectangle).
            if (m_activeDragWindow != null && m_activeDragIsMouse && !m_leftButtonDown)
            {
                return null;
            }

            var windowDragPreview =
                m_currentInteraction == UserInteraction.Moving
                || (m_currentInteraction == UserInteraction.Starting && m_activeDragWindow != null)
                || (m_currentInteraction == UserInteraction.None && m_activeDragWindow != null);
            if (m_movingPanelNode == null && !windowDragPreview)
            {
                return null;
            }

            // Safety net: suppress cues if the drag source became floating mid-drag
            // (e.g. via hotkey or exclusion-list update while dragging).
            if (m_activeDragWindow != null)
            {
                using (m_floatingSetLock.EnterScope())
                {
                    if (m_floatingSet.Contains(m_activeDragWindow))
                        return null;
                }
            }

            try
            {
                if (IsSwapModifierPressed())
                {
                    return null;
                }

                var pt = m_workspace.CursorLocation;
                using (m_backendLock.EnterScope())
                {
                    var exclude = GetDragExcludeWindows();
                    var targetWindow = m_backend.WindowAtPointForDrag(
                        m_workspace.VirtualDesktopManager.CurrentDesktop,
                        pt,
                        exclude,
                        m_activeDragWindow);
                    if (targetWindow == null)
                    {
                        return null;
                    }

                    if (m_activeDragWindow != null)
                    {
                        var sourceWindow = m_backend.FindWindow(m_activeDragWindow);
                        // Suppress cues only for same-stack drags. Same split-parent drags can still
                        // create left/right/top/bottom outcomes and should keep cues visible.
                        if (sourceWindow != null
                            && sourceWindow.Parent is StackPanelNode sourceStack
                            && ReferenceEquals(sourceStack, targetWindow.Parent))
                        {
                            // Same stack drag does not create a new split/stack outcome.
                            return null;
                        }
                    }

                    var zone = targetWindow.Parent is StackPanelNode
                        ? TilingWorkspace.DropZone.Center
                        : TilingWorkspace.ClassifyDropZone(targetWindow.ComputedRectangle, pt);
                    TilingWorkspace.GetDropZoneHighlightRects(
                        targetWindow.ComputedRectangle,
                        zone,
                        out var center,
                        out var left,
                        out var top,
                        out var right,
                        out var bottom);
                    var previewKind = zone switch
                    {
                        TilingWorkspace.DropZone.Center => DropZonePreviewKind.Center,
                        TilingWorkspace.DropZone.Left => DropZonePreviewKind.Left,
                        TilingWorkspace.DropZone.Right => DropZonePreviewKind.Right,
                        TilingWorkspace.DropZone.Top => DropZonePreviewKind.Top,
                        TilingWorkspace.DropZone.Bottom => DropZonePreviewKind.Bottom,
                        TilingWorkspace.DropZone.Neutral => DropZonePreviewKind.Neutral,
                        _ => DropZonePreviewKind.Neutral,
                    };
                    return new DropZonePreviewState(
                        IsActive: true,
                        ActiveZone: previewKind,
                        Center: center,
                        Left: left,
                        Top: top,
                        Right: right,
                        Bottom: bottom,
                        TargetOutline: targetWindow.ComputedRectangle);
                }
            }
            catch (InvalidWindowReferenceException)
            {
            }
            catch (Exception ex)
            {
                m_logger.Warning(ex, "Failed to compute drop-zone preview state");
            }

            return null;
        }

        private void MoveToParentPanel(TilingNode node)
        {
            try
            {
                using (m_backendLock.EnterScope())
                {
                    m_backend.PullUp(node);
                }
                InvalidateLayout();
            }
            catch (TilingFailedException e)
            {
                m_logger.Error(e, "Attempted pull up of {Node} failed", node);
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(e.FailReason));
            }
        }

        private void WrapInSplitPanel(TilingNode node, bool vertical)
        {
            try
            {
                using (m_backendLock.EnterScope())
                {
                    m_backend.WrapInSplitPanel(node, vertical);
                    m_backend.SetFocus(node);

                    node.Parent!.Padding = GetPanelPaddingRect();
                    node.Parent!.Spacing = GetPanelSpacing();

                    if (m_allocateNewPanelSpace)
                    {
                        node.Parent!.Attach(new PlaceholderNode());
                    }

                    InvalidateLayout();
                }
            }
            catch (TilingFailedException ex)
            {
                m_logger.Error(ex, "Attempted split of {Node} failed", node);
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason));
            }
        }

        private void WrapInStackPanel(TilingNode node)
        {
            try
            {
                using (m_backendLock.EnterScope())
                {
                    m_backend.WrapInStackPanel(node);
                    node.Parent!.Padding = GetPanelPaddingRect();
                    node.Parent!.Spacing = GetPanelSpacing();
                    m_backend.SetFocus(node);
                    InvalidateLayout();
                }
            }
            catch (TilingFailedException ex)
            {
                m_logger.Error(ex, "Attempted stack of {Node} failed", node);
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason));
            }
        }

        private IntPtr GetOverlayAnchor()
        {
            // COM VD class factory can fail after sleep/wake while Explorer
            // re-registers (REGDB_E_CLASSNOTREG — GitHub #447). Return no
            // anchor; the overlay loop will retry on the next tick.
            IVirtualDesktop desktop;
            try
            {
                desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                // Throttle: GetOverlayAnchor runs on every overlay tick, so log at most
                // once per 5s while the VD COM service is unregistered (sleep/wake).
                if (m_sw.Elapsed - m_lastAnchorComWarning > TimeSpan.FromSeconds(5))
                {
                    m_lastAnchorComWarning = m_sw.Elapsed;
                    m_logger.Warning(ex, "Virtual desktop COM unavailable while resolving overlay anchor; skipping anchor this tick");
                }
                return new IntPtr(0);
            }
            using (m_backendLock.EnterScope())
            {
                try
                {
                    var focusedNode = m_backend.GetFocus(desktop);
                    if (focusedNode is WindowNode window)
                        return window.WindowReference.Handle;
                }
                catch (ArgumentException)
                {
                    return new IntPtr(0);
                }
            }

            var comparer = m_workspace.CreateSnapshotZOrderComparer();
            using (m_backendLock.EnterScope())
            {
                var tree = m_backend.GetTree(desktop);
                if (tree == null)
                    return new IntPtr(0);
                var topWindow = tree.Root!.Windows
                    .OrderByDescending(x => x.WindowReference, comparer)
                    .FirstOrDefault();

                if (topWindow != null)
                    return topWindow.WindowReference.Handle;

                return new IntPtr(0);
            }
        }

        private void ToggleFloat(IWindow window)
        {
            bool floated;
            using (m_floatingSetLock.EnterScope())
            {
                if (m_floatingSet.Contains(window))
                {
                    floated = false;
                    m_floatingSet.Remove(window);
                }
                else
                {
                    floated = true;
                    m_floatingSet.Add(window);
                }
            }
            // User explicitly toggled float — remove from retry tracking so
            // RetryFailedPlacements() won't override the user's intent.
            using (m_placementFailedSetLock.EnterScope())
            {
                m_placementFailedSet.Remove(window);
            }
            DetectChanges(window);
            if (floated)
            {
                OnWindowFloated(window);
            }
            else
            {
                try
                {
                    using (m_backendLock.EnterScope())
                    {
                        m_backend.SetFocus(window);
                    }
                }
                catch
                {
                }
            }
        }

        private void OnDisplayScalingChanged(object? sender, DisplayScalingChangedEventArgs e)
        {
            PropagatePanelHeightChange();
        }

        private void OnPlacementFailed(object? sender, TilingFailedEventArgs e)
        {
            if (e.FailReason == TilingError.NoValidPlacementExists && e.FailSource != null)
            {
                using (m_floatingSetLock.EnterScope())
                {
                    m_floatingSet.Add(e.FailSource);
                }
                // Mark as auto-floated so RetryFailedPlacements() can re-attempt
                // once transient constraints (e.g. post-hibernation) resolve.
                using (m_placementFailedSetLock.EnterScope())
                {
                    m_placementFailedSet.Add(e.FailSource);
                }
                OnWindowFloated(e.FailSource);
            }
        }

        /// Re-attempts tiling for windows that were auto-floated due to transient
        /// constraint failures (e.g. stale min/max sizes right after hibernation
        /// resume or display reconnect). Called on a delay to give Windows time to
        /// stabilize display geometry and window metrics.
        internal void RetryFailedPlacements()
        {
            List<IWindow> candidates;
            using (m_placementFailedSetLock.EnterScope())
            {
                candidates = [.. m_placementFailedSet];
            }

            if (candidates.Count == 0)
                return;

            m_logger.Information("Retrying placement for {Count} auto-floated window(s)", candidates.Count);

            foreach (var window in candidates)
            {
                try
                {
                    // Un-float so DetectChanges → CanManage → RegisterWindow path runs.
                    using (m_floatingSetLock.EnterScope())
                    {
                        m_floatingSet.Remove(window);
                    }
                    using (m_placementFailedSetLock.EnterScope())
                    {
                        m_placementFailedSet.Remove(window);
                    }

                    // DetectChanges will re-register if constraints now permit it.
                    // If it still fails, OnPlacementFailed re-adds to both sets.
                    DetectChanges(window);
                }
                catch (InvalidWindowReferenceException)
                {
                    // Window was destroyed between scheduling the retry and now.
                }
            }
        }

        private void OnWindowFloated(IWindow window)
        {
            Rectangle? originalPosition;
            try
            {
                using (m_backendLock.EnterScope())
                {
                    originalPosition = m_backend.GetOriginalPosition(window);
                }
            }
            catch
            {
                originalPosition = null;
            }
            try
            {
                originalPosition ??= GetOptimalRestoredSize(window);

                var originalDisplay = m_workspace.DisplayManager.Displays.FirstOrDefault(x => x.Bounds.Contains(originalPosition.Value.Center));
                originalDisplay ??= m_workspace.DisplayManager.PrimaryDisplay;

                var displayBounds = originalDisplay.Bounds;

                var centeredPosition = Rectangle.OffsetAndSize(
                    displayBounds.Left + displayBounds.Width / 2 - originalPosition.Value.Width / 2,
                    displayBounds.Top + displayBounds.Height / 2 - originalPosition.Value.Height / 2,
                    originalPosition.Value.Width,
                    originalPosition.Value.Height);

                window.SetPosition(centeredPosition);
                FocusHelper.ForceActivate(window.Handle);
            }
            catch (Exception e) when (e is InvalidWindowReferenceException || e is InvalidOperationException && window.State != WindowState.Restored)
            {
                // ignore
            }
        }

        private Rectangle GetOptimalRestoredSize(IWindow window)
        {
            var screenSize = m_display.WorkArea.Size;
            var minSize = window.MinSize ?? new Point(0, 0);
            var maxSize = window.MaxSize ?? new Point(screenSize.X, screenSize.Y);
            var pos = window.Position;

            return Rectangle.OffsetAndSize(
                pos.Left,
                pos.Top,
                Math.Max(minSize.X, Math.Min(maxSize.X, Math.Min(screenSize.X, (screenSize.X + minSize.X) / 2))),
                Math.Max(minSize.Y, Math.Min(maxSize.Y, Math.Min(screenSize.Y, (screenSize.Y + minSize.Y) / 2))));
        }


        private void OnCursorLocationChanged(object? sender, CursorLocationChangedEventArgs e)
        {
            // Keep drag previews responsive to cursor movement even when some windows
            // don't emit position-changed events continuously during title-bar drag.
            // Skip when border-resize gesture is active (WM_NCHITTEST classified it).
            if (!m_borderResizeGesture
                && m_currentInteraction != UserInteraction.Resizing
                && (m_activeDragWindow != null || m_movingPanelNode != null))
            {
                InvalidateLayout();
            }

            if (PendingIntent == null)
                return;

            m_dispatcher.BeginInvoke(() =>
            {
                if (PendingIntent is GroupWithIntent gwi)
                {
                    if (Mouse.LeftButton != MouseButtonState.Pressed)
                    {
                        PendingIntent.Cancel();
                        PendingIntent = null;
                    }

                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.NodeAtPoint(m_workspace.VirtualDesktopManager.CurrentDesktop, e.NewLocation) is WindowNode targetNode)
                        {
                            var newSet = new HashSet<IWindow> { gwi.Source.WindowReference, targetNode.WindowReference };
                            if (!m_gui.PreviewWindows.SetEquals(newSet))
                            {
                                m_gui.PreviewWindows = newSet;
                            }
                        }
                    }
                }
            });
        }

        private void OnPendingIntentChanged(object? sender, EventArgs e)
        {
            if (PendingIntent == null)
            {
                _ = m_dispatcher.BeginInvoke(() =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
            }
            else
            {
                if (App.Current.Services.GetService<LowLevelMouseHook>() is LowLevelMouseHook mshk)
                {
                    var startPt = m_workspace.CursorLocation;
                    bool dispatched = false;
                    void onMouseButtonChanged(object? sender, ref LowLevelMouseHook.ButtonStateChangedEventArgs e)
                    {
                        mshk.ButtonStateChanged -= onMouseButtonChanged;
                        if (e.Button == LowLevelMouseHook.MouseButton.Left && e.IsPressed == false)
                        {
                            var pt = new Point(e.X, e.Y);
                            if (Math.Abs(pt.X - startPt.X) > 5 || Math.Abs(pt.Y - startPt.Y) > 5)
                            {
                                if (!dispatched)
                                {
                                    dispatched = true;
                                    m_dispatcher.BeginInvoke(() =>
                                    {
                                        HitTestCompletePendingIntent(pt);
                                    });
                                }
                            }
                        }
                        else
                        {
                            if (!dispatched)
                            {
                                dispatched = true;
                                m_dispatcher.BeginInvoke(() =>
                                {
                                    PendingIntent?.Cancel();
                                    PendingIntent = null;
                                });
                            }
                        }
                    }
                    mshk.ButtonStateChanged += onMouseButtonChanged;
                }
            }
        }

        private void HitTestCompletePendingIntent(Point cursorPosition)
        {
            if (m_pendingIntent is GroupWithIntent intent && m_display.Bounds.Contains(cursorPosition))
            {
                PendingIntent = null;

                WindowNode sourceNode;
                PanelNode panel;
                using (m_backendLock.EnterScope())
                {
                    var node = m_backend.NodeAtPoint(m_workspace.VirtualDesktopManager.CurrentDesktop, cursorPosition);
                    if (node is not WindowNode targetNode)
                    {
                        intent.Cancel();
                        return;
                    }
                    if (targetNode.WindowReference.Equals(intent.Source.WindowReference))
                    {
                        intent.Cancel();
                        return;
                    }

                    switch (intent.Type)
                    {
                        case GroupWithIntent.GroupType.HorizontalPanel:
                            if (!CanSplit(targetNode))
                            {
                                intent.Cancel();
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NestingInStackPanel, targetNode.WindowReference));
                                return;
                            }
                            break;
                        case GroupWithIntent.GroupType.VerticalPanel:
                            if (!CanSplit(targetNode))
                            {
                                intent.Cancel();
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NestingInStackPanel, targetNode.WindowReference));
                                return;
                            }
                            break;
                        case GroupWithIntent.GroupType.StackPanel:
                            if (!CanStack(targetNode))
                            {
                                intent.Cancel();
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(TilingError.NestingInStackPanel, targetNode.WindowReference));
                                return;
                            }
                            break;
                    }

                    // Must complete before doing anything with the intent data.
                    intent.Complete();
                    sourceNode = intent.Source;

                    switch (intent.Type)
                    {
                        case GroupWithIntent.GroupType.HorizontalPanel:
                            m_backend.WrapInSplitPanel(targetNode, vertical: false);
                            break;
                        case GroupWithIntent.GroupType.VerticalPanel:
                            m_backend.WrapInSplitPanel(targetNode, vertical: true);
                            break;
                        case GroupWithIntent.GroupType.StackPanel:
                            m_backend.WrapInStackPanel(targetNode);
                            break;
                    }
                    panel = targetNode.Parent!;
                    panel.Spacing = GetPanelSpacing();
                    panel.Padding = GetPanelPaddingRect();
                }


                BindEventHandlers(sourceNode.WindowReference);
                using (m_windowSetLock.EnterScope())
                {
                    m_windowSet.Add(sourceNode.WindowReference);
                }
                if (CanManage(sourceNode.WindowReference))
                {
                    //m_logger.Information("Window {Handle}={ProcessName} can be managed, registering with backend", e.Source.Handle, e.Source.GetCachedProcessName());
                    try
                    {
                        try
                        {
                            using (m_backendLock.EnterScope())
                            {
                                var node = m_backend.RegisterWindow(sourceNode.WindowReference, panel);
                                m_backend.SetFocus(node);
                            }
                        }
                        catch (NoValidPlacementExistsException)
                        {
                            PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                                TilingError.NoValidPlacementExists, sourceNode.WindowReference));
                        }
                    }
                    catch
                    {
                        return;
                    }

                    InvalidateLayout();
                }
            }
            else
            {
                m_pendingIntent?.Cancel();
            }
        }

        private void OnBeginHorizontalWithRequestedAsync(object? sender, WindowNode e)
        {
            m_gui.PreviewWindows = new HashSet<IWindow> { e.WindowReference };
            PendingIntent = new GroupWithIntent(GroupWithIntent.GroupType.HorizontalPanel, e,
                complete: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                    OnWindowRemoved(this, new WindowChangedEventArgs(e.WindowReference));
                },
                cancel: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
        }

        private void OnBeginVerticalWithRequested(object? sender, WindowNode e)
        {
            m_gui.PreviewWindows = new HashSet<IWindow> { e.WindowReference };
            PendingIntent = new GroupWithIntent(GroupWithIntent.GroupType.VerticalPanel, e,
                complete: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                    OnWindowRemoved(this, new WindowChangedEventArgs(e.WindowReference));
                },
                cancel: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
        }

        private void OnBeginStackWithRequested(object? sender, WindowNode e)
        {
            m_gui.PreviewWindows = new HashSet<IWindow> { e.WindowReference };
            PendingIntent = new GroupWithIntent(GroupWithIntent.GroupType.StackPanel, e,
                complete: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                    OnWindowRemoved(this, new WindowChangedEventArgs(e.WindowReference));
                },
                cancel: () =>
                {
                    m_gui.PreviewWindows = EmptyWindowSet;
                });
        }

        private void OnWindowVerticalSplitRequested(object? sender, TilingNode e)
        {
            WrapInSplitPanel(e, true);
        }

        private void OnWindowStackRequested(object? sender, TilingNode e)
        {
            WrapInStackPanel(e);
        }

        private void OnWindowPullUpRequested(object? sender, TilingNode e)
        {
            MoveToParentPanel(e);
        }

        private void OnWindowHorizontalSplitRequested(object? sender, TilingNode e)
        {
            WrapInSplitPanel(e, false);
        }

        private void OnWindowFloatRequested(object? sender, WindowNode e)
        {
            ToggleFloat(e.WindowReference);
        }

        private void OnWindowIgnoreProcessRequested(object? sender, WindowNode e)
        {
            App.Current.AppState.Settings.SaveAsync(x =>
            {
                return x with { ProcessIgnoreList = [.. x.ProcessIgnoreList, e.WindowReference.GetCachedProcessName()] };
            });
        }
        private void OnWindowIgnoreClassRequested(object? sender, WindowNode e)
        {
            App.Current.AppState.Settings.SaveAsync(x =>
            {
                return x with { ClassIgnoreList = [.. x.ClassIgnoreList, ((WinMan.Windows.Win32Window)e.WindowReference).ClassName] };
            });
        }

        private void OnTilingPanelMoving(object? sender, PanelNode panel)
        {
            m_currentInteraction = UserInteraction.Moving;
            m_movingPanelNode = panel;
            InvalidateLayout();
        }

        private void OnTilingPanelMoveRequested(object? sender, PanelNode panel)
        {
            m_logger.Information("Panel {Panel} move ended", panel);
            m_currentInteraction = UserInteraction.None;
            m_movingPanelNode = null;

            try
            {
                var isSwapping = IsSwapModifierPressed();
                var pt = m_workspace.CursorLocation;
                using (m_backendLock.EnterScope())
                {
                    // Check that panel hasn't disappeared during the move.
                    if (panel.Desktop == null)
                    {
                        return;
                    }
                    m_backend.MoveNode(
                        panel,
                        pt,
                        allowNesting: m_enableDragDropAutoPanelCreation && !isSwapping,
                        swapOnDrop: isSwapping);
                }

                InvalidateLayout();
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
            catch (TilingFailedException e)
            {
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(e.FailReason));
            }
        }

        private void OnTilingNodePullUpRequested(object? sender, TilingNode node)
        {
            MoveToParentPanel(node);
        }

        private void OnDesktopAdded(object? sender, DesktopChangedEventArgs e)
        {
            m_logger.Information("Desktop {Desktop} added to workspace", e.Source);
            var orientation = m_display.Bounds.Width >= m_display.Bounds.Height ? PanelOrientation.Horizontal : PanelOrientation.Vertical;
            using (m_backendLock.EnterScope())
            {
                m_backend.RegisterDesktop(e.Source, m_display.WorkArea, orientation);
            }
        }

        private void OnDesktopRemoved(object? sender, DesktopChangedEventArgs e)
        {
            m_logger.Information("Desktop {Desktop} removed from workspace", e.Source);
            using (m_backendLock.EnterScope())
            {
                m_backend.UnregisterDesktop(e.Source);
            }
        }

        private void OnCurrentDesktopChanged(object? sender, CurrentDesktopChangedEventArgs e)
        {
            Refresh();
            InvalidateLayout();
        }

        private void OnWindowGotFocus(object? sender, WindowFocusChangedEventArgs e)
        {
            m_dispatcher.BeginInvoke(() =>
            {
                m_logger.Information("Got focus on {Window}", e.Source.DebugString());
                try
                {
                    bool hideMaximised = false;
                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.HasWindow(e.Source))
                        {
                            m_logger.Debug("Window {Window} is managed by backend, need to hide all obstructing windows", e.Source.DebugString());
                            // Focused restored windows that are in the tree cause all maximised windows
                            // to be send to the back
                            hideMaximised = true;
                            m_backend.SetFocus(e.Source);
                        }
                        else
                        {
                            m_logger.Debug("Window {Window} is not managed by backend", e.Source.DebugString());
                            return;
                        }
                    }

                    if (hideMaximised)
                    {
                        m_logger.Debug("Moving all obstructing maximised windows to back");
                        var comparer = m_workspace.CreateSnapshotZOrderComparer();
                        foreach (var maximisedWindow in m_workspace.GetCurrentDesktopSnapshot()
                            .Where(x => x.State == WindowState.Maximized && m_display.Bounds.Contains(x.Position.Center))
                            .OrderBy(x => x, comparer))
                        {
                            m_logger.Information("Moving maximised window {Window} to back", maximisedWindow.DebugString());
                            try
                            {
                                if (maximisedWindow.CanReorder)
                                {
                                    maximisedWindow.SendToBack();
                                }
                            }
                            catch (InvalidWindowReferenceException)
                            {
                                continue;
                            }
                            catch (Win32Exception ex)
                            {
                                m_logger.Error(ex, "Moving window {Window} to back failed ({@Metadata})", maximisedWindow.DebugString(), maximisedWindow.GetMetadata());
                                continue;
                            }
                        }
                    }
                    InvalidateLayout();
                }
                catch (InvalidWindowReferenceException)
                {
                    return;
                }
            }, System.Windows.Threading.DispatcherPriority.DataBind);
        }

        private void OnWindowLostFocus(object? sender, WindowFocusChangedEventArgs e)
        {
            // This delay is needed to handle the case where the previously focused window
            // loses focus because another window was just created and the OnWindowAdded event
            // observes the new window as focused.
            //m_logger.Information("Lost focus on {Handle}={ProcessName}", e.Source.Handle, e.Source.GetCachedProcessName());
            //await Task.Delay(250);

            //SilenceExceptionIfDead(() =>
            //{
            //    using (m_backendLock.EnterScope())
            //    {
            //        if (m_backend.HasWindow(e.Source))
            //        {
            //            m_logger.Information("Removing focus from {Handle}={ProcessName}", e.Source.Handle, e.Source.GetCachedProcessName());
            //            m_backend.UnsetFocus(e.Source);
            //            InvalidateLayout();
            //        }
            //    }
            //});
            // During move drags, focus can move to hover target windows. Keep drag interaction
            // state alive while we still have an active drag source so drop cues don't disappear.
            if (m_activeDragWindow == null)
            {
                m_currentInteraction = UserInteraction.None;
            }
        }

        private void OnWindowAdded(object? sender, WindowChangedEventArgs e)
        {
            m_logger.Debug("Window {Window} added to workspace", e.Source.DebugString());
            try
            {
                BindEventHandlers(e.Source);
                using (m_windowSetLock.EnterScope())
                {
                    m_windowSet.Add(e.Source);
                }
                using (m_newWindowSetLock.EnterScope())
                {
                    m_newWindowSet.Add(e.Source);
                }

                if (m_exclusionMatchers.Any(x => x.Matches(e.Source)))
                {
                    using (m_floatingSetLock.EnterScope())
                    {
                        m_floatingSet.Add(e.Source);
                    }
                }

                if (!AutoRegisterWindows)
                {
                    return;
                }

                if (m_autoFloatNewWindows)
                {
                    using (m_floatingSetLock.EnterScope())
                    {
                        m_floatingSet.Add(e.Source);
                    }
                }

                if (CanManage(e.Source) && e.Source.State == WindowState.Restored)
                {
                    m_logger.Information("Window {Window} can be managed, registering with backend ({Display})", e.Source.DebugString(), m_display);
                    m_dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            try
                            {
                                using (m_backendLock.EnterScope())
                                {
                                    if (m_backend.HasWindow(e.Source))
                                    {
                                        return;
                                    }

                                    var node = m_backend.RegisterWindow(e.Source, m_autoSplitCount, m_overflowPlacementStrategy);
                                    node.Parent!.Padding = GetPanelPaddingRect();
                                    node.Parent!.Spacing = GetPanelSpacing();
                                }
                            }
                            catch (NoValidPlacementExistsException)
                            {
                                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                                    TilingError.NoValidPlacementExists, e.Source));
                            }
                        }
                        catch
                        {
                            return;
                        }

                        InvalidateLayout();
                    }, System.Windows.Threading.DispatcherPriority.DataBind);
                }
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
        }

        private void OnWindowRemoved(object? sender, WindowChangedEventArgs e)
        {
            m_logger.Information("Window {Window} removed from workspace", e.Source.DebugString());

            // The drag source vanished mid-gesture (e.g. it closed itself while being
            // dragged). No PositionChangeEnd will arrive to clear the gesture state, so
            // the drop-zone/preview cues would stay stuck on screen. Reset it here.
            if (ReferenceEquals(m_activeDragWindow, e.Source))
            {
                m_activeDragWindow = null;
                m_activeDragIsMouse = false;
                m_borderResizeGesture = false;
                m_currentInteraction = UserInteraction.None;
                InvalidateLayout();
            }

            UnbindEventHandlers(e.Source);
            using (m_savedLocationsLock.EnterScope())
            {
                m_savedLocations.Remove(e.Source);
            }
            using (m_ignoreRepositionSetLock.EnterScope())
            {
                m_ignoreRepositionSet.Remove(e.Source);
            }
            using (m_backendLock.EnterScope())
            {
                if (m_backend.HasWindow(e.Source))
                {
                    m_logger.Debug("Unregistering window {Window} from backend", e.Source.DebugString());
                    m_backend.UnregisterWindow(e.Source);
                    InvalidateLayout();
                }
            }
            using (m_floatingSetLock.EnterScope())
            {
                m_floatingSet.Remove(e.Source);
            }
            using (m_placementFailedSetLock.EnterScope())
            {
                m_placementFailedSet.Remove(e.Source);
            }
            using (m_newWindowSetLock.EnterScope())
            {
                m_newWindowSet.Remove(e.Source);
            }
            using (m_windowSetLock.EnterScope())
            {
                m_windowSet.Remove(e.Source);
            }
        }

        private void DoWindowMove(IWindow window)
        {
            var isSwapping = IsSwapModifierPressed();
            var pt = m_workspace.CursorLocation;
            using (m_backendLock.EnterScope())
            {
                if (m_backend.HasWindow(window))
                {
                    m_logger.Debug("Window {Window} size is unchanged, attempting to insert window at {Position}", window.DebugString(), pt);
                    try
                    {
                    m_backend.MoveWindow(
                        window,
                        pt,
                        allowNesting: m_enableDragDropAutoPanelCreation && !isSwapping,
                        swapOnDrop: isSwapping);
                        m_backend.SetFocus(window);
                    }
                    catch (TilingFailedException ex)
                    {
                        m_logger.Warning(
                            "MoveWindow tiling failed: {Reason} window={Window} cursor={Cursor}",
                            ex.FailReason,
                            window.DebugString(),
                            pt);
                        throw;
                    }
                }
            }
        }

        private void OnWindowPositionChangeEnd(object? sender, WindowPositionChangedEventArgs e)
        {
            if (!m_active)
                return;

            if (m_delayReposition && m_currentInteraction == UserInteraction.Moving)
            {
                try
                {
                    DoWindowMove(e.Source);
                }
                catch (InvalidWindowReferenceException)
                {
                }
                catch (TilingFailedException ex)
                {
                    PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason, e.Source));
                }
            }

            m_logger.Information("Window {Window} move ended", e.Source.DebugString());
            InvalidateLayout();
            using (m_ignoreRepositionSetLock.EnterScope())
            {
                m_ignoreRepositionSet.Remove(e.Source);
            }

            m_activeDragWindow = null;
            m_activeDragIsMouse = false;
            m_borderResizeGesture = false;
            m_currentInteraction = UserInteraction.None;
        }

        private TimeSpan m_lastPlacementFailed = TimeSpan.Zero;
        private TimeSpan m_lastWindowPositionChanged = TimeSpan.Zero;

        private void OnWindowPositionChanged(object? sender, WindowPositionChangedEventArgs e)
        {
            if (!m_active)
                return;

            if (m_sw.Elapsed - m_lastPlacementFailed <= TimeSpan.FromMilliseconds(100))
            {
                return;
            }

            if (m_currentInteraction != UserInteraction.None && m_sw.Elapsed - m_lastWindowPositionChanged <= TimeSpan.FromSeconds(1.0 / m_display.RefreshRate))
            {
                return;
            }
            m_lastWindowPositionChanged = m_sw.Elapsed;

            using (m_ignoreRepositionSetLock.EnterScope())
            {
                if (!m_ignoreRepositionSet.Contains(e.Source))
                {
                    // Some other event might have resulted in the movement of the window.
                    // Do not call DetectChanges under the lock, to avoid deadlock.
                    m_dispatcher.InvokeAsync(() => DetectChanges(e.Source));
                    return;
                }
            }

            using (m_backendLock.EnterScope())
            {
                if (!m_backend.HasWindow(e.Source))
                {
                    return;
                }
            }

            if (m_currentInteraction == UserInteraction.Starting)
            {
                if (e.OldPosition.Size == e.NewPosition.Size)
                {
                    m_currentInteraction = UserInteraction.Moving;
                }
                else
                {
                    m_currentInteraction = UserInteraction.Resizing;
                }
            }

            try
            {
                DetectChanges(e.Source);

                if (e.NewPosition == e.OldPosition)
                {
                    return;
                }

                if (e.NewPosition.Width == e.OldPosition.Width && e.NewPosition.Height == e.OldPosition.Height)
                {
                    if (!m_delayReposition)
                    {
                        DoWindowMove(e.Source);
                    }
                }
                else
                {
                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.HasWindow(e.Source))
                        {
                            var node = m_backend.FindWindow(e.Source);
                            var oldPosition = node!.ComputedContentRectangle;
                            var frame = e.Source.FrameMargins;
                            var adjustedRect = new Rectangle(
                                left: oldPosition.Left - frame.Left,
                                top: oldPosition.Top - frame.Top,
                                right: oldPosition.Right + frame.Right,
                                bottom: oldPosition.Bottom + frame.Bottom);

                            m_logger.Debug("Window {Window} size is different, attempting to resize window from {OldPosition} to {NewPosition}", e.Source.DebugString(), adjustedRect, e.NewPosition);
                            m_backend.ResizeWindow(e.Source, e.NewPosition, adjustedRect);
                            UpdateTree(node.Desktop!);
                        }
                    }
                }
                InvalidateLayout();
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
            catch (TilingFailedException ex)
            {
                if (m_sw.Elapsed - m_lastPlacementFailed <= TimeSpan.FromSeconds(1))
                {
                    return;
                }
                m_lastPlacementFailed = m_sw.Elapsed;
                PlacementFailed?.Invoke(this, new TilingFailedEventArgs(ex.FailReason, e.Source));
            }
            finally
            {
                Unfreeze();
            }
        }

        private void OnWindowTopmostChanged(object? sender, WindowTopmostChangedEventArgs e)
        {
            if (!m_active)
                return;

            try
            {
                m_logger.Verbose("Changed topmost of window {Window}", e.Source.DebugString());
                DetectChanges(e.Source);
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
        }

        private void OnWindowStateChanged(object? sender, WindowStateChangedEventArgs e)
        {
            if (!m_active)
                return;

            void UnregisterAndSaveLocation()
            {
                using (m_backendLock.EnterScope())
                {
                    var window = m_backend.FindWindow(e.Source);
                    if (window != null)
                    {
                        using (m_savedLocationsLock.EnterScope())
                        {
                            // Be resilient to multiple OnWindowStateChanged events happening one after the other
                            m_savedLocations[e.Source] = new NodeLocation(window);
                        }
                        InvalidateLayout();
                        m_backend.UnregisterWindow(e.Source);
                    }
                }
                DetectChanges(e.Source);
            }

            void RegisterAndRestoreLocation()
            {
                NodeLocation? savedLocation;
                using (m_savedLocationsLock.EnterScope())
                {
                    if (m_savedLocations.TryGetValue(e.Source, out savedLocation))
                    {
                        m_savedLocations.Remove(e.Source);
                    }
                }

                void RegisterInTopLevelPanel()
                {
                    try
                    {
                        var window = m_backend.RegisterWindow(e.Source, m_autoSplitCount, m_overflowPlacementStrategy);
                        window.Parent!.Padding = GetPanelPaddingRect();
                        window.Parent!.Spacing = GetPanelSpacing();
                    }
                    catch (WindowAlreadyRegisteredException)
                    {
                        // Window might be already registered!
                        var registered = m_backend.FindWindow(e.Source);
                        if (registered == null)
                        {
                            throw;
                        }
                        // This is clearly a race condition with DetectChanges dirty checking.
                    }
                }

                void RegisterInSavedPanel()
                {
                    WindowNode window;
                    try
                    {
                        window = m_backend.RegisterWindow(e.Source, savedLocation.Parent);
                        window.Parent!.Padding = GetPanelPaddingRect();
                        window.Parent!.Spacing = GetPanelSpacing();
                    }
                    catch (WindowAlreadyRegisteredException)
                    {
                        // Window might be already registered!
                        var registered = m_backend.FindWindow(e.Source);
                        if (registered == null)
                        {
                            throw;
                        }
                        // This is clearly a race condition with DetectChanges dirty checking.
                        window = registered;
                    }

                    window.Parent!.Detach(window);
                    int childCount = savedLocation.Parent.Children.Count;
                    int index = Math.Min(savedLocation.Index, childCount);
                    savedLocation.Parent.Attach(index, window);

                    // Restore size
                    if (window.Parent is GridLikeNode gridNode)
                    {
                        if (m_backend.GetTree(m_workspace.VirtualDesktopManager.CurrentDesktop) is DesktopTree tree)
                        {
                            // Assign ComputedRectangle to that Resize will work.
                            try
                            {
                                tree.Measure();
                                tree.Arrange();
                            }
                            catch (UnsatisfiableFlexConstraintsException)
                            {
                            }
                            if (gridNode.CanResizeInOrientation(PanelOrientation.Horizontal))
                            {
                                gridNode.ResizeTo(window, savedLocation.ComputedRectangle.Width, GrowDirection.Both);
                            }
                            else
                            {
                                gridNode.ResizeTo(window, savedLocation.ComputedRectangle.Height, GrowDirection.Both);
                            }
                        }
                    }
                }

                try
                {
                    using (m_backendLock.EnterScope())
                    {
                        if (savedLocation?.Parent?.Desktop != null)
                        {
                            try
                            {
                                RegisterInSavedPanel();
                            }
                            catch (NoValidPlacementExistsException)
                            {
                                RegisterInTopLevelPanel();
                            }
                        }
                        else
                        {
                            RegisterInTopLevelPanel();
                        }
                    }
                }
                catch (NoValidPlacementExistsException)
                {
                    PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                        TilingError.NoValidPlacementExists, e.Source));
                }
                DetectChanges(e.Source);
            }

            try
            {
                m_logger.Information("Changed state of window {Window} to {NewState}", e.Source.DebugString(), e.NewState);

                try
                {
                    // Window is now minimized or maximized but was restored
                    if ((e.NewState == WindowState.Maximized || e.NewState == WindowState.Minimized)
                        && e.OldState == WindowState.Restored)
                    {
                        UnregisterAndSaveLocation();
                    }
                    // Window is now restored
                    else if (e.NewState == WindowState.Restored
                        && (e.OldState == WindowState.Maximized || e.OldState == WindowState.Minimized))
                    {
                        if (!CanManage(e.Source))
                        {
                            return;
                        }
                        RegisterAndRestoreLocation();
                    }
                    else
                    {
                        DetectChanges(e.Source);
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    return;
                }
                catch (WindowAlreadyRegisteredException)
                {
                    return;
                }

                if (Equals(m_workspace.FocusedWindow, sender))
                {
                    m_logger.Debug("Window {Window} is also focused, calling OnWindowGotFocus", e.Source.DebugString());
                    // This is to update focus when a maximised window is restored.
                    OnWindowGotFocus(e.Source, new WindowFocusChangedEventArgs(e.Source, true));
                }
            }
            catch (InvalidWindowReferenceException)
            {
                return;
            }
        }

        private void OnWindowPositionChangeStart(object? sender, WindowPositionChangedEventArgs e)
        {
            if (!m_active)
                return;

            using (m_ignoreRepositionSetLock.EnterScope())
            {
                m_ignoreRepositionSet.Add(e.Source);
            }

            // Only windows actually tiled by THIS backend may trigger drag-drop cues. Anything
            // not in the tree — floating, topmost, non-resizable, pinned, off-display, or managed
            // by another display's service — can't participate in panel creation, so it must not
            // set m_activeDragWindow (which would light up drop cues over tiled windows).
            using (m_backendLock.EnterScope())
            {
                if (!m_backend.HasWindow(e.Source))
                    return;
            }

            m_activeDragWindow = e.Source;
            m_activeDragIsMouse = m_leftButtonDown;
            // Classify gesture at start: WM_NCHITTEST tells us if the cursor is
            // over a sizing border, so we can suppress drag-drop cues during resize.
            m_borderResizeGesture = NcHitTest.IsBorderResize(e.Source.Handle);
            m_currentInteraction = UserInteraction.Starting;
            InvalidateLayout();
        }

        private void OnTilingNodeFocusRequested(object? sender, TilingNode e)
        {
            using (m_backendLock.EnterScope())
            {
                var windowNode = e.Windows.FirstOrDefault();
                try
                {
                    if (windowNode != null)
                    {
                        if (FocusHelper.ForceActivate(windowNode.WindowReference.Handle))
                        {
                            m_backend.SetFocus(windowNode.WindowReference);
                        }
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    return;
                }
            }
        }

        private void OnTilingNodeCloseRequested(object? sender, TilingNode e)
        {
            foreach (var window in e.Windows.ToList())
            {
                try
                {
                    if (window.WindowReference.CanClose)
                    {
                        window.WindowReference.Close();
                    }
                }
                catch (InvalidWindowReferenceException)
                {
                    // Ignore
                }
                catch (Win32Exception)
                {
                    // Ignore
                    // TODO: Show toast
                }
            }
        }

        private void BindEventHandlers(IWindow window)
        {
            window.StateChanged += OnWindowStateChanged;
            window.PositionChangeStart += OnWindowPositionChangeStart;
            window.PositionChangeEnd += OnWindowPositionChangeEnd;
            window.PositionChanged += OnWindowPositionChanged;
            window.GotFocus += OnWindowGotFocus;
            window.LostFocus += OnWindowLostFocus;
            window.TopmostChanged += OnWindowTopmostChanged;
        }

        private void UnbindEventHandlers(IWindow window)
        {
            window.StateChanged -= OnWindowStateChanged;
            window.PositionChangeStart -= OnWindowPositionChangeStart;
            window.PositionChangeEnd -= OnWindowPositionChangeEnd;
            window.PositionChanged -= OnWindowPositionChanged;
            window.GotFocus -= OnWindowGotFocus;
            window.LostFocus -= OnWindowLostFocus;
            window.TopmostChanged -= OnWindowTopmostChanged;
        }

        private static readonly TimeSpan StuckDragRecoveryDelay = TimeSpan.FromMilliseconds(350);

        private void SubscribeGlobalMouseHook()
        {
            if (App.Current.Services.GetService<LowLevelMouseHook>() is LowLevelMouseHook hook)
            {
                m_mouseHook = hook;
                m_mouseHook.ButtonStateChanged += OnGlobalMouseButtonStateChanged;
            }
        }

        private void UnsubscribeGlobalMouseHook()
        {
            if (m_mouseHook != null)
            {
                m_mouseHook.ButtonStateChanged -= OnGlobalMouseButtonStateChanged;
                m_mouseHook = null;
            }
        }

        private void OnGlobalMouseButtonStateChanged(object? sender, ref LowLevelMouseHook.ButtonStateChangedEventArgs e)
        {
            if (e.Button != LowLevelMouseHook.MouseButton.Left)
                return;

            if (e.IsPressed)
            {
                m_leftButtonDown = true;
                return;
            }

            m_leftButtonDown = false;

            // Button released => any mouse drag is over, so hide the drag cues DIRECTLY and
            // unconditionally. Don't route this through the layout recompute / GetDropZonePreviewState
            // gate — that path can be throttled or never run (e.g. when a window never emits
            // PositionChangeEnd), which left the cue stuck on screen. Clearing the GUI cue does not
            // touch gesture state, so a pending delayed placement still completes in PositionChangeEnd.
            _ = m_dispatcher.InvokeAsync(() =>
            {
                m_gui.DropZonePreview = null;
                m_gui.PreviewRectangle = null;
            });

            // Separately, recover stuck gesture state after a short grace period so PositionChangeEnd
            // can win the race for the real placement first. No-ops when nothing needs recovery.
            _ = m_dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(StuckDragRecoveryDelay);
                ClearStuckDragStateIfIdle();
            });
        }

        private void ClearStuckDragStateIfIdle()
        {
            // A fresh press started a new gesture — leave it alone.
            if (m_leftButtonDown)
                return;
            // Panel moves are driven by WPF mouse capture and clear themselves reliably.
            if (m_movingPanelNode != null)
                return;
            if (m_activeDragWindow == null && m_currentInteraction == UserInteraction.None && !m_borderResizeGesture)
                return;

            // In DelayReposition mode the actual placement runs in OnWindowPositionChangeEnd,
            // gated on m_currentInteraction == Moving. EVENT_SYSTEM_MOVESIZEEND (which drives
            // that handler) can arrive well after the physical button-up for slow/busy windows.
            // Do NOT clear the interaction here — that would skip DoWindowMove and silently drop
            // the placement. But DO refresh layout so the button-up gate hides the released cue
            // (the cue must not stay stuck while we wait for the late PositionChangeEnd).
            if (m_delayReposition && m_currentInteraction == UserInteraction.Moving && m_activeDragWindow != null)
            {
                InvalidateLayout();
                return;
            }

            m_logger.Debug("Recovering stuck drag-gesture state after mouse release");
            m_activeDragWindow = null;
            m_activeDragIsMouse = false;
            m_borderResizeGesture = false;
            m_currentInteraction = UserInteraction.None;
            InvalidateLayout();
        }

        private bool IsSwapModifierPressed()
        {
            static bool GetState() => Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            if (m_dispatcher.CheckAccess())
            {
                return GetState();
            }
            else
            {
                return m_dispatcher.Invoke(GetState, System.Windows.Threading.DispatcherPriority.Send);
            }
        }

        private bool DetectChanges(IWindow window)
        {
            m_logger.Verbose("Dirty checking for changes with window {Window}", window.DebugString());
            try
            {
                if (window.State == WindowState.Restored && CanManage(window))
                {
                    if (!AutoRegisterWindows)
                    {
                        return false;
                    }

                    try
                    {
                        using (m_backendLock.EnterScope())
                        {
                            try
                            {
                                if (!m_backend.HasWindow(window))
                                {
                                    m_logger.Debug("Window {Window} can be managed, but is not registered with backend, registering now", window.DebugString());
                                    var newNode = m_backend.RegisterWindow(window, m_autoSplitCount, m_overflowPlacementStrategy);
                                    newNode.Parent!.Padding = GetPanelPaddingRect();
                                    newNode.Parent!.Spacing = GetPanelSpacing();
                                    InvalidateLayout();
                                    return true;
                                }
                            }
                            catch (InvalidWindowReferenceException)
                            {
                                if (m_backend.HasWindow(window))
                                    m_backend.UnregisterWindow(window);
                            }
                        }
                    }
                    catch (NoValidPlacementExistsException)
                    {
                        PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                            TilingError.NoValidPlacementExists, window));
                    }
                }
                else
                {
                    using (m_backendLock.EnterScope())
                    {
                        if (m_backend.HasWindow(window))
                        {
                            m_logger.Verbose("Window {Window} can no longer be managed, but is registered with backend, unregistering now", window.DebugString());
                            m_backend.UnregisterWindow(window);

                            InvalidateLayout();
                            return true;
                        }
                    }
                }
            }
            catch (WindowAlreadyRegisteredException)
            {
                return false;
            }
            // TODO: Is the following catch block necessary?
            catch (InvalidOperationException)
            {
                return false;
            }
            return false;
        }

        private bool CanManage(IWindow x, bool ignoreFloating = false)
        {
            bool IsOnCurrentDisplay()
            {
                var pos = x.Position.Center;
                if (m_display.Bounds.Contains(pos))
                    return true;

                // Check if on any other displays
                return !m_workspace.DisplayManager.Displays
                    .Where(d => !d.Equals(m_display) && d.Bounds.Contains(pos))
                    .Any();
            }
            bool IsFloating()
            {
                using (m_floatingSetLock.EnterScope())
                {
                    return m_floatingSet.Contains(x);
                }
            }

            // Cheap boolean read
            if (x.IsTopmost)
            {
                return false;
            }

            // Set lookup
            if (!ignoreFloating && IsFloating())
            {
                return false;
            }

            // GetWindowPos + Lookup
            if (!IsOnCurrentDisplay())
            {
                return false;
            }

            // GetWindowStyle + OpenProcess
            if (!x.CanResize)
            {
                return false;
            }

            // OpenProcess (expensive)
            if (!x.CanMove)
            {
                return false;
            }

            // Virtual Desktop stuff is very expensive.
            // The COM VD service can throw transiently during input-sync calls,
            // display changes, or hibernation resume (GitHub #450, #457).
            // Safe default: don't manage the window when we can't determine pin state.
            try
            {
                if (m_workspace.VirtualDesktopManager.IsWindowPinned(x))
                {
                    return false;
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                m_logger.Verbose(ex, "Virtual desktop pin-state query failed for {Window}; treating as unmanageable this pass", x.DebugString());
                return false;
            }

            return true;
        }

        private void InvalidateLayout()
        {
            if (!m_active)
            {
                return;
            }

            m_dirty = true;
            if (m_frozen.IsPositive())
            {
                return;
            }
            m_dispatcher.InvokeAsync(new Action(() =>
            {
                if (!m_dirty || m_frozen.IsPositive())
                    return;
                m_dirty = false;
                _ = UpdateLayoutAsync();
            }), System.Windows.Threading.DispatcherPriority.DataBind);
        }

        private void Freeze()
        {
            m_frozen.Increment();
        }

        private void Unfreeze()
        {
            if (m_frozen.DecrementIfPositive())
            {
                if (m_dirty)
                {
                    InvalidateLayout();
                }
            }
        }

        private static Rectangle ShrinkTo(Rectangle container, int width, int height)
        {
            int wdiff = container.Width - width;
            int hdiff = container.Height - height;
            return new Rectangle(
                container.Left + wdiff / 2,
                container.Top + hdiff / 2,
                container.Right - wdiff / 2,
                container.Height - wdiff / 2
            );
        }

        private int GetPanelSpacing()
        {
            double scaling = m_display.Scaling;
            return (int)(m_windowPadding * scaling);
        }

        private Rectangle GetPanelPaddingRect()
        {
            double scaling = m_display.Scaling;
            return new Rectangle(0, (int)((m_panelHeight + m_windowPadding) * scaling), 0, 0);
        }

        private static System.Windows.Thickness ToThickness(Rectangle rc)
        {
            return new System.Windows.Thickness(rc.Left, rc.Top, rc.Right, rc.Bottom);
        }

        private void UpdateGuiNodeOptions()
        {
            m_dispatcher.Invoke(() =>
            {
                m_gui.PanelSpacing = GetPanelSpacing();
                m_gui.PanelPadding = ToThickness(GetPanelPaddingRect());
                m_gui.InvalidateView();
                InvalidateLayout();
            });
        }

        private void PropagatePaddingChange()
        {
            using (m_backendLock.EnterScope())
            {
                foreach (var tree in m_backend.Trees)
                {
                    SyncPanelChromeMetrics(tree);
                }
            }
            UpdateGuiNodeOptions();
        }

        private void PropagatePanelHeightChange()
        {
            using (m_backendLock.EnterScope())
            {
                foreach (var tree in m_backend.Trees)
                {
                    SyncPanelChromeMetrics(tree);
                }
            }
            UpdateGuiNodeOptions();
        }

        private void PropagateShowFocusChange()
        {
            InvalidateLayout();
        }

        private void PropagateShowPreviewFocusChange()
        {
            InvalidateLayout();
        }

        bool HasFocusAndAdjacentWindow(TilingDirection direction)
        {
            try
            {
                using (m_backendLock.EnterScope())
                {
                    m_backend.GetFocusAndAdjacentWindow(m_workspace.VirtualDesktopManager.CurrentDesktop, direction);
                    return true;
                }
            }
            catch (TilingFailedException e) when (e.FailReason == TilingError.MissingTarget || e.FailReason == TilingError.MissingAdjacentWindow)
            {
                return false;
            }
        }
    }
}
