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
            lock (m_backend)
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

        private void UpdateTree(DesktopTree tree)
        {
            tree.WorkArea = m_display.WorkArea;

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
                    lock (m_floatingSet)
                    {
                        m_floatingSet.Add(largestWindow.WindowReference);
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

            List<TilingNode> snapshot;
            IReadOnlyCollection<TilingNode> focusedPath;
            TilingNode? focusedNode;
            IVirtualDesktop desktop;
            DesktopTree tree;

            lock (m_backend)
            {
                desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
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
                    lock (m_ignoreRepositionSet)
                    {
                        snapshotWindows = snapshot.OfType<WindowNode>().Where(x => !m_ignoreRepositionSet.Contains(x.WindowReference)).ToList();
                    }

                    bool useSmoothing = AnimateWindowMovement && m_currentInteraction != UserInteraction.Resizing;
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
            lock (m_newWindowSet)
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
            if (m_currentInteraction == UserInteraction.Moving && DelayReposition || m_movingPanelNode != null)
            {
                try
                {
                    var isSwapping = IsSwapModifierPressed();
                    var pt = m_workspace.CursorLocation;

                    if (m_movingPanelNode == null)
                    {
                        var window = m_workspace.FocusedWindow;
                        if (window == null)
                        {
                            return null;
                        }

                        lock (m_backend)
                        {
                            if (m_backend.HasWindow(window))
                            {
                                return m_backend.MockMoveWindow(window, pt, allowNesting: !isSwapping).preArrange;
                            }
                        }
                    }
                    else
                    {
                        lock (m_backend)
                        {
                            var rect = m_backend.MockMoveNode(m_movingPanelNode, pt, allowNesting: !isSwapping).preArrange;
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
            }
            return null;
        }

        private void MoveToParentPanel(TilingNode node)
        {
            try
            {
                lock (m_backend)
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
                lock (m_backend)
                {
                    m_backend.WrapInSplitPanel(node, vertical);
                    m_backend.SetFocus(node);

                    node.Parent!.Padding = GetPanelPaddingRect();
                    node.Parent!.Spacing = GetPanelSpacing();

                    if (AllocateNewPanelSpace)
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
                lock (m_backend)
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
            var desktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            lock (m_backend)
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

                var tree = m_backend.GetTree(desktop);
                if (tree == null)
                    return new IntPtr(0);
                var comparer = m_workspace.CreateSnapshotZOrderComparer();
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
            lock (m_floatingSet)
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
            DetectChanges(window);
            if (floated)
            {
                OnWindowFloated(window);
            }
            else
            {
                try
                {
                    lock (m_backend)
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
                lock (m_floatingSet)
                {
                    m_floatingSet.Add(e.FailSource);
                }
                OnWindowFloated(e.FailSource);
            }
        }

        private void OnWindowFloated(IWindow window)
        {
            Rectangle? originalPosition;
            try
            {
                lock (m_backend)
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

                    lock (m_backend)
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
                lock (m_backend)
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
                lock (m_windowSet)
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
                            lock (m_backend)
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
                var x2 = x.Clone();
                x2.ProcessIgnoreList = [.. x2.ProcessIgnoreList, e.WindowReference.GetCachedProcessName()];
                return x2;
            });
        }
        private void OnWindowIgnoreClassRequested(object? sender, WindowNode e)
        {
            App.Current.AppState.Settings.SaveAsync(x =>
            {
                var x2 = x.Clone();
                x2.ClassIgnoreList = [.. x2.ClassIgnoreList, ((WinMan.Windows.Win32Window)e.WindowReference).ClassName];
                return x2;
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
                lock (m_backend)
                {
                    // Check that panel hasn't disappeared during the move.
                    if (panel.Desktop == null)
                    {
                        return;
                    }
                    m_backend.MoveNode(panel, pt, allowNesting: !isSwapping);
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
            lock (m_backend)
            {
                m_backend.RegisterDesktop(e.Source, orientation);
            }
        }

        private void OnDesktopRemoved(object? sender, DesktopChangedEventArgs e)
        {
            m_logger.Information("Desktop {Desktop} removed from workspace", e.Source);
            lock (m_backend)
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
                    lock (m_backend)
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
            //    lock (m_backend)
            //    {
            //        if (m_backend.HasWindow(e.Source))
            //        {
            //            m_logger.Information("Removing focus from {Handle}={ProcessName}", e.Source.Handle, e.Source.GetCachedProcessName());
            //            m_backend.UnsetFocus(e.Source);
            //            InvalidateLayout();
            //        }
            //    }
            //});
            m_currentInteraction = UserInteraction.None;
        }

        private void OnWindowAdded(object? sender, WindowChangedEventArgs e)
        {
            m_logger.Debug("Window {Window} added to workspace", e.Source.DebugString());
            try
            {
                BindEventHandlers(e.Source);
                lock (m_windowSet)
                {
                    m_windowSet.Add(e.Source);
                }
                lock (m_newWindowSet)
                {
                    m_newWindowSet.Add(e.Source);
                }

                if (m_exclusionMatchers.Any(x => x.Matches(e.Source)))
                {
                    lock (m_floatingSet)
                    {
                        m_floatingSet.Add(e.Source);
                    }
                }

                if (!AutoRegisterWindows)
                {
                    return;
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
                                lock (m_backend)
                                {
                                    var node = m_backend.RegisterWindow(e.Source, maxTreeWidth: AutoSplitCount);
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

            UnbindEventHandlers(e.Source);
            lock (m_windowSet)
            {
                m_windowSet.Remove(e.Source);
            }
            lock (m_newWindowSet)
            {
                m_newWindowSet.Remove(e.Source);
            }
            lock (m_floatingSet)
            {
                m_floatingSet.Remove(e.Source);
            }
            lock (m_savedLocations)
            {
                m_savedLocations.Remove(e.Source);
            }
            lock (m_ignoreRepositionSet)
            {
                m_ignoreRepositionSet.Remove(e.Source);
            }

            lock (m_backend)
            {
                if (m_backend.HasWindow(e.Source))
                {
                    m_logger.Debug("Unregistering window {Window} from backend", e.Source.DebugString());
                    m_backend.UnregisterWindow(e.Source);
                    InvalidateLayout();
                }
            }
        }

        private void DoWindowMove(IWindow window)
        {
            var isSwapping = IsSwapModifierPressed();
            var pt = m_workspace.CursorLocation;
            lock (m_backend)
            {
                if (m_backend.HasWindow(window))
                {
                    m_logger.Debug("Window {Window} size is unchanged, attempting to insert window at {Position}", window.DebugString(), pt);
                    m_backend.MoveWindow(window, pt, allowNesting: !isSwapping);
                    m_backend.SetFocus(window);
                }
            }
        }

        private void OnWindowPositionChangeEnd(object? sender, WindowPositionChangedEventArgs e)
        {
            if (!m_active)
                return;

            if (DelayReposition && m_currentInteraction == UserInteraction.Moving)
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
            lock (m_ignoreRepositionSet)
            {
                m_ignoreRepositionSet.Remove(e.Source);
            }
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

            lock (m_ignoreRepositionSet)
            {
                if (!m_ignoreRepositionSet.Contains(e.Source))
                {
                    // Some other event might have resulted in the movement of the window.
                    // Do not call DetectChanges under the lock, to avoid deadlock.
                    m_dispatcher.InvokeAsync(() => DetectChanges(e.Source));
                    return;
                }
            }

            lock (m_backend)
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
                    if (!DelayReposition)
                    {
                        DoWindowMove(e.Source);
                    }
                }
                else
                {
                    lock (m_backend)
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
                lock (m_backend)
                {
                    var window = m_backend.FindWindow(e.Source);
                    if (window != null)
                    {
                        lock (m_savedLocations)
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
                lock (m_savedLocations)
                {
                    if (m_savedLocations.TryGetValue(e.Source, out savedLocation))
                    {
                        m_savedLocations.Remove(e.Source);
                    }
                }

                void RegisterInTopLevelPanel()
                {
                    var window = m_backend.RegisterWindow(e.Source, maxTreeWidth: AutoSplitCount);
                    window.Parent!.Padding = GetPanelPaddingRect();
                    window.Parent!.Spacing = GetPanelSpacing();
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
                    lock (m_backend)
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

            lock (m_ignoreRepositionSet)
            {
                m_ignoreRepositionSet.Add(e.Source);
            }
            m_currentInteraction = UserInteraction.Starting;
        }

        private void OnTilingNodeFocusRequested(object? sender, TilingNode e)
        {
            lock (m_backend)
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
                        lock (m_backend)
                        {
                            try
                            {
                                if (!m_backend.HasWindow(window))
                                {
                                    m_logger.Debug("Window {Window} can be managed, but is not registered with backend, registering now", window.DebugString());
                                    var newNode = m_backend.RegisterWindow(window, maxTreeWidth: AutoSplitCount);
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
                    lock (m_backend)
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
                lock (m_floatingSet)
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

            // Virtual Desktop stuff is very expensive
            if (m_workspace.VirtualDesktopManager.IsWindowPinned(x))
            {
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
            lock (m_backend)
            {
                foreach (var panel in m_backend.Trees.SelectMany(x => x.Root!.Nodes).OfType<PanelNode>())
                {
                    panel.Spacing = GetPanelSpacing();
                }
            }
            UpdateGuiNodeOptions();
        }

        private void PropagatePanelHeightChange()
        {
            lock (m_backend)
            {
                foreach (var panel in m_backend.Trees.SelectMany(x => x.Root!.Nodes).OfType<PanelNode>())
                {
                    panel.Padding = GetPanelPaddingRect();
                    panel.Spacing = GetPanelSpacing();
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
                lock (m_backend)
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
