using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using WinMan;
using System;
using FancyWM.Models;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using Serilog;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;

namespace FancyWM
{
    /// <summary>
    /// Manages the layout of window in the workspace
    /// </summary>
    internal partial class TilingService : ITilingService, IDisposable
    {
        private enum UserInteraction
        {
            None,
            Starting,
            Moving,
            Resizing,
        }

        private class NodeLocation(TilingNode node)
        {
            public PanelNode Parent = node.Parent ?? throw new ArgumentException(nameof(node));
            public int Index = node.Parent.IndexOf(node);
            public Rectangle ComputedRectangle = node.ComputedRectangle;
        }

        public event EventHandler<TilingFailedEventArgs>? PlacementFailed;
        public event EventHandler<EventArgs>? PendingIntentChanged;

        /// <summary>
        /// Current active state.
        /// <see cref="Start"/>
        /// <see cref="Stop"/>
        /// </summary>
        public bool Active
        {
            get => m_active;
        }

        public bool AutoRegisterWindows { get; internal set; }

        private bool m_allocateNewPanelSpace;

        private bool m_animateWindowMovement;

        private int m_autoSplitCount = 100;

        private bool m_delayReposition = false;

        private void SetAutoCollapse(bool value)
        {
            m_backend.AutoCollapse = value;
        }

        private void SetWindowPadding(int value)
        {
            m_windowPadding = value;
            PropagatePaddingChange();
        }

        private void SetPanelHeight(int value)
        {
            m_panelHeight = value;
            PropagatePanelHeightChange();
        }

        private void SetShowFocus(bool value)
        {
            m_showFocus = value;
            PropagateShowFocusChange();
        }

        public bool ShowPreviewFocus
        {
            get => m_showPreviewFocus;
            set
            {
                m_showPreviewFocus = value;
                PropagateShowPreviewFocusChange();
            }
        }

        public IWorkspace Workspace => m_workspace;

        public IReadOnlyCollection<IWindowMatcher> ExclusionMatchers
        {
            get => m_exclusionMatchers;
            set
            {
                m_exclusionMatchers = [.. value];

                lock (m_windowSet)
                {
                    foreach (var window in m_windowSet)
                    {
                        if (m_exclusionMatchers.Any(x => x.Matches(window)))
                        {
                            lock (m_floatingSet)
                            {
                                m_floatingSet.Add(window);
                            }
                        }
                    }
                }
                Refresh();
            }
        }

        public ITilingServiceIntent? PendingIntent
        {
            get => m_pendingIntent;
            set
            {
                if (m_pendingIntent != value)
                {
                    m_pendingIntent = value;
                    PendingIntentChanged?.Invoke(this, new EventArgs());
                }
            }
        }

        private static readonly IReadOnlySet<IWindow> EmptyWindowSet = new HashSet<IWindow>();

        /// <summary>
        /// The dispatcher from the thread that created the <see cref="TilingService"/>
        /// </summary>
        private readonly Dispatcher m_dispatcher;
        private readonly IWorkspace m_workspace;
        private readonly ILogger m_logger = App.Current.Logger;
        private IReadOnlyCollection<IWindowMatcher> m_exclusionMatchers = [];

        private readonly TilingOverlayRenderer m_gui;
        private readonly TilingWorkspace m_backend;
        private readonly IDisplay m_display;
        private readonly HashSet<IWindow> m_newWindowSet = [];
        private readonly HashSet<IWindow> m_windowSet = [];
        private readonly HashSet<IWindow> m_floatingSet = [];
        private readonly HashSet<IWindow> m_ignoreRepositionSet = [];
        private readonly Dictionary<IWindow, NodeLocation> m_savedLocations = [];

        private readonly CompositeDisposable m_subscriptions = [];
        private readonly IAnimationThread m_animationThread;
        private int m_panelHeight = 20;
        private int m_windowPadding = 2;
        private bool m_showFocus = false;
        private bool m_showPreviewFocus = false;

        private bool m_active = false;
        private bool m_dirty = true;
        private UserInteraction m_currentInteraction = UserInteraction.None;
        private PanelNode? m_movingPanelNode;
        private ITilingServiceIntent? m_pendingIntent;
        private readonly Counter m_frozen = new();
        private readonly Stopwatch m_sw = new();

        public TilingService(IWorkspace workspace, IDisplay display, IAnimationThread animationThread, IObservable<ITilingServiceSettings> settings, bool autoRegisterWindows)
        {
            m_logger.Information("Managing display {Display} (Bounds: {Bounds}, Scale: {Scaling})", display, display.Bounds, display.Scaling);
            m_dispatcher = Dispatcher.CurrentDispatcher;
            m_workspace = workspace;
            m_animationThread = animationThread;
            m_display = display;
            m_backend = new TilingWorkspace();
            m_gui = new TilingOverlayRenderer(display, GetOverlayAnchor)
            {
                PanelSpacing = GetPanelSpacing(),
                PanelPadding = ToThickness(GetPanelPaddingRect()),
            };
            m_gui.TilingNodeFocusRequested += OnTilingNodeFocusRequested;
            m_gui.TilingNodeCloseRequested += OnTilingNodeCloseRequested;
            m_gui.TilingNodePullUpRequested += OnTilingNodePullUpRequested;
            m_gui.TilingPanelMoving += OnTilingPanelMoving;
            m_gui.TilingPanelMoveRequested += OnTilingPanelMoveRequested;
            m_gui.BeginHorizontalWithRequested += OnBeginHorizontalWithRequestedAsync;
            m_gui.BeginVerticalWithRequested += OnBeginVerticalWithRequested;
            m_gui.BeginStackWithRequested += OnBeginStackWithRequested;
            m_gui.FloatRequested += OnWindowFloatRequested;
            m_gui.HorizontalSplitRequested += OnWindowHorizontalSplitRequested;
            m_gui.VerticalSplitRequested += OnWindowVerticalSplitRequested;
            m_gui.PullUpRequested += OnWindowPullUpRequested;
            m_gui.StackRequested += OnWindowStackRequested;
            m_gui.IgnoreProcessRequested += OnWindowIgnoreProcessRequested;
            m_gui.IgnoreClassRequested += OnWindowIgnoreClassRequested;

            AutoRegisterWindows = autoRegisterWindows;

            foreach (var d in m_workspace.VirtualDesktopManager.Desktops)
            {
                OnDesktopAdded(this, new DesktopChangedEventArgs(d));
            }

            m_workspace.VirtualDesktopManager.DesktopAdded += OnDesktopAdded;
            m_workspace.VirtualDesktopManager.DesktopRemoved += OnDesktopRemoved;
            m_workspace.VirtualDesktopManager.CurrentDesktopChanged += OnCurrentDesktopChanged;
            m_workspace.CursorLocationChanged += OnCursorLocationChanged;

            m_display.ScalingChanged += OnDisplayScalingChanged;

            m_workspace.WindowAdded += OnWindowAdded;
            m_workspace.WindowRemoved += OnWindowRemoved;

            PlacementFailed += OnPlacementFailed;
            PendingIntentChanged += OnPendingIntentChanged;

            m_subscriptions.Add(m_gui);
            m_subscriptions.Add(settings.Subscribe(OnSettingsChanged));

            var currentDesktop = m_workspace.VirtualDesktopManager.CurrentDesktop;
            OnCurrentDesktopChanged(this, new CurrentDesktopChangedEventArgs(currentDesktop, currentDesktop));

            var tree = m_backend.GetTree(currentDesktop)!;
            foreach (var w in m_workspace.GetSnapshot())
            {
                OnWindowAdded(w, new WindowChangedEventArgs(w));
                if (m_backend.HasWindow(w))
                {
                    m_backend.SetFocus(w);
                    UpdateTree(tree);
                }
            }

            m_sw.Start();
        }

        private void OnSettingsChanged(ITilingServiceSettings x)
        {
            _ = m_dispatcher.RunAsync(() =>
            {
                m_allocateNewPanelSpace = x.AllocateNewPanelSpace;
                m_animateWindowMovement = x.AnimateWindowMovement;
                m_autoSplitCount = x.AutoSplitCount;
                m_delayReposition = x.DelayReposition;
                SetWindowPadding(x.WindowPadding);
                SetPanelHeight(x.PanelHeight);
                SetShowFocus(x.ShowFocus);
                SetAutoCollapse(x.AutoCollapsePanels);
            });
        }

        public void Start()
        {
            m_active = true;
            InvalidateLayout();
            m_gui.Show();
        }

        public void Stop()
        {
            m_active = false;
            RestoreOriginalLayout();
            m_gui.Hide();
        }

        public bool CanMoveFocus(TilingDirection direction)
        {
            return HasFocusAndAdjacentWindow(direction);
        }

        public void MoveFocus(TilingDirection direction)
        {
            lock (m_backend)
            {
                var adjacentWindow = m_backend.GetFocusAdjacentWindow(m_workspace.VirtualDesktopManager.CurrentDesktop, direction);
                if (FocusHelper.ForceActivate(adjacentWindow.WindowReference.Handle))
                {
                    m_backend.SetFocus(adjacentWindow);
                }
            }

            InvalidateLayout();
        }

        public bool CanMoveWindow(TilingDirection direction)
        {
            return HasFocusAndAdjacentWindow(direction);
        }

        public void MoveWindow(TilingDirection direction)
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop) ?? throw new TilingFailedException(TilingError.MissingTarget);
                WindowNode? adjacentWindow = focusedNode.GetAdjacentWindow(direction) ?? throw new TilingFailedException(TilingError.MissingAdjacentWindow);
                var adjancentWindowIndex = adjacentWindow.Parent!.IndexOf(adjacentWindow);

                if (adjacentWindow.Parent == focusedNode.Parent)
                {
                    var focusedNodeIndex = focusedNode.Parent.IndexOf(focusedNode);
                    var adjacentNodeIndex = focusedNode.Parent.IndexOf(adjacentWindow);
                    focusedNode.Parent.Move(focusedNodeIndex, adjacentNodeIndex);
                }
                else
                {

                    if (direction == TilingDirection.Left || direction == TilingDirection.Up)
                    {
                        m_backend.MoveAfter(focusedNode, adjacentWindow);
                    }
                    else
                    {
                        m_backend.MoveBefore(focusedNode, adjacentWindow);
                    }
                }
            }

            InvalidateLayout();
        }

        public bool CanSwapFocus(TilingDirection direction)
        {
            return HasFocusAndAdjacentWindow(direction);
        }

        public void SwapFocus(TilingDirection direction)
        {
            lock (m_backend)
            {
                (var currentWindow, var adjacentWindow) = m_backend.GetFocusAndAdjacentWindow(m_workspace.VirtualDesktopManager.CurrentDesktop, direction);
                currentWindow!.Swap(adjacentWindow);
            }
            InvalidateLayout();
        }

        public bool DiscoverWindows()
        {
            if (!AutoRegisterWindows)
            {
                return false;
            }

            List<IWindow> windows;
            lock (m_windowSet)
            {
                windows = [.. m_windowSet];
            }

            bool anyChanges = false;
            foreach (var window in windows)
            {
                lock (m_backend)
                {
                    try
                    {
                        if (!m_backend.HasWindow(window) && window.State == WindowState.Restored && CanManage(window))
                        {
                            m_logger.Debug("Discovered window {Window}", window.DebugString());
                            var newNode = m_backend.RegisterWindow(window, maxTreeWidth: m_autoSplitCount);
                            newNode.Parent!.Padding = GetPanelPaddingRect();
                            newNode.Parent!.Spacing = GetPanelSpacing();
                            InvalidateLayout();
                            anyChanges = true;
                        }
                    }
                    catch (NoValidPlacementExistsException)
                    {
                        PlacementFailed?.Invoke(this, new TilingFailedEventArgs(
                            TilingError.NoValidPlacementExists, window));
                    }
                    catch (InvalidWindowReferenceException)
                    {
                        if (m_backend.HasWindow(window))
                            m_backend.UnregisterWindow(window);
                    }
                }
            }

            return anyChanges;
        }

        public async void Refresh()
        {
            List<IWindow> windows;
            lock (m_windowSet)
            {
                windows = [.. m_windowSet];
            }

            bool anyChanges = false;
            foreach (var window in windows)
            {
                if (await DetectChangesAsync(window))
                {
                    anyChanges = true;
                }
            }

            if (anyChanges)
            {
                InvalidateLayout();
            }

            List<IWindow> movedWindows = [];

            lock (m_backend)
            {
                foreach (var desktop in m_workspace.VirtualDesktopManager.Desktops)
                {
                    var tree = m_backend.GetTree(desktop);
                    if (tree == null)
                        continue;
                    foreach (var window in windows)
                    {
                        if (tree.FindNode(window) != null && !desktop.HasWindow(window))
                        {
                            movedWindows.Add(window);
                        }
                    }
                }
            }

            foreach (var movedWindow in movedWindows)
            {
                OnWindowRemoved(movedWindow, new WindowChangedEventArgs(movedWindow));
                OnWindowAdded(movedWindow, new WindowChangedEventArgs(movedWindow));
            }
        }

        private static bool CanSplit(TilingNode node)
        {
            return !node.PathToRoot.OfType<StackPanelNode>().Any();
        }

        public bool CanSplit(bool vertical)
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
                return focusedNode != null && CanSplit(focusedNode);
            }
        }

        public void Split(bool vertical)
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop) ?? throw new TilingFailedException(TilingError.MissingTarget);
                WrapInSplitPanel(focusedNode, vertical);
                m_backend.SetFocus(focusedNode);
            }
        }

        public bool CanFloat()
        {
            var window = m_workspace.FocusedWindow;
            return window != null && CanManage(window, ignoreFloating: true);
        }

        public void Float()
        {
            var window = m_workspace.FocusedWindow ?? throw new TilingFailedException(TilingError.MissingTarget);
            ToggleFloat(window);
        }

        private static bool CanStack(TilingNode node)
        {
            return !node.PathToRoot.OfType<StackPanelNode>().Any();
        }

        public bool CanStack()
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
                return focusedNode != null && CanStack(focusedNode);
            }
        }

        public void Stack()
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
                if (focusedNode == null || focusedNode.Parent == null)
                    throw new TilingFailedException(TilingError.MissingTarget);

                WrapInStackPanel(focusedNode);
            }
        }

        public bool CanPullUp()
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
                return focusedNode != null && focusedNode.Parent != focusedNode.Desktop!.Root;
            }
        }

        public void PullUp()
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop) ?? throw new TilingFailedException(TilingError.MissingTarget);
                MoveToParentPanel(focusedNode);
                m_backend.SetFocus(focusedNode);
            }
        }

        public async void ToggleDesktop()
        {
            if (m_active)
            {
                Stop();
                await Task.Delay(50);
                foreach (var window in m_workspace.GetCurrentDesktopSnapshot())
                {
                    try
                    {
                        if (window.CanMinimize)
                            window.SetState(WindowState.Minimized);
                    }
                    catch (Exception e) when (e is Win32Exception || e is InvalidWindowReferenceException)
                    {
                        // Ignore
                    }
                }
            }
            else
            {
                foreach (var window in m_workspace.GetCurrentDesktopSnapshot())
                {
                    try
                    {
                        if (window.CanMinimize)
                            window.SetState(WindowState.Restored);
                    }
                    catch (Exception e) when (e is Win32Exception || e is InvalidWindowReferenceException)
                    {
                        // Ignore
                    }
                }
                await Task.Delay(50);
                Start();
                Refresh();
            }
        }

        public void Dispose()
        {
            m_logger.Information("No longer managing display {Display}", m_display);

            m_active = false;
            m_subscriptions.Dispose();

            PlacementFailed = null;

            m_workspace.VirtualDesktopManager.DesktopAdded -= OnDesktopAdded;
            m_workspace.VirtualDesktopManager.DesktopRemoved -= OnDesktopRemoved;
            m_workspace.VirtualDesktopManager.CurrentDesktopChanged -= OnCurrentDesktopChanged;
            m_workspace.CursorLocationChanged -= OnCursorLocationChanged;

            m_workspace.WindowAdded -= OnWindowAdded;
            m_workspace.WindowRemoved -= OnWindowRemoved;

            m_display.ScalingChanged -= OnDisplayScalingChanged;

            // There is still the possibility that OnWindowAdded gets called, but hopefully that does not happen too often.
            lock (m_windowSet)
            {
                foreach (var window in m_windowSet)
                {
                    UnbindEventHandlers(window);
                }
            }
        }

        public bool CanResize(PanelOrientation orientation, double displayPercentage)
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
                if (focusedNode is not WindowNode focusedWindow)
                    return false;

                var window = focusedWindow.WindowReference;
                var oldSize = window.Position;
                var display = m_workspace.DisplayManager.Displays.FirstOrDefault(x => x.WorkArea.Contains(window.Position.Center));
                if (display == null)
                    return false;

                var verticalDelta = (int)(display.WorkArea.Height * displayPercentage);
                var horizontalDelta = (int)(display.WorkArea.Width * displayPercentage);

                var grandparent = focusedNode.Ancestors
                        .Select(x => x as GridLikeNode)
                        .Where(x => x != null)
                        .FirstOrDefault(x => x!.CanResizeInOrientation(orientation));
                if (grandparent != null)
                {
                    double newSize;
                    switch (orientation)
                    {
                        case PanelOrientation.Horizontal:
                            newSize = oldSize.Width + horizontalDelta / 2;
                            return focusedNode.Parent!.GetMaxChildSize(focusedNode).X > newSize;
                        case PanelOrientation.Vertical:
                            newSize = oldSize.Height + verticalDelta / 2;
                            return focusedNode.Parent!.GetMaxChildSize(focusedNode).Y > newSize;
                        default:
                            throw new NotImplementedException();
                    }
                }

                return false;
            }
        }

        public void Resize(PanelOrientation orientation, double displayPercentage)
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
                if (focusedNode is not WindowNode focusedWindow)
                    throw new TilingFailedException(TilingError.MissingTarget);

                var window = focusedWindow.WindowReference;
                var oldSize = window.Position;
                var display = m_workspace.DisplayManager.Displays.FirstOrDefault(x => x.WorkArea.Contains(window.Position.Center)) ?? throw new TilingFailedException(TilingError.Failed);
                var verticalDelta = (int)(display.WorkArea.Height * displayPercentage);
                var horizontalDelta = (int)(display.WorkArea.Width * displayPercentage);
                var newSize = orientation switch
                {
                    PanelOrientation.Horizontal => new Rectangle(oldSize.Left - horizontalDelta / 2, oldSize.Top, oldSize.Right + horizontalDelta / 2, oldSize.Bottom),
                    PanelOrientation.Vertical => new Rectangle(oldSize.Left, oldSize.Top - verticalDelta / 2, oldSize.Right, oldSize.Bottom + verticalDelta / 2),
                    _ => throw new NotImplementedException(),
                };
                m_backend.ResizeWindow(window, newSize, oldSize);
            }
            InvalidateLayout();
        }

        public IWindow? GetFocus()
        {
            lock (m_backend)
            {
                var focusedNode = m_backend.GetFocus(m_workspace.VirtualDesktopManager.CurrentDesktop);
                if (focusedNode is not WindowNode focusedWindow)
                    return null;

                return focusedWindow.WindowReference;
            }
        }

        public Rectangle GetBounds()
        {
            return m_display.Bounds;
        }

        public IWindow? FindClosest(Point center)
        {
            static double Distance(Point point1, Point point2)
            {
                return Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2);
            }

            lock (m_backend)
            {
                var tree = m_backend.GetTree(m_workspace.VirtualDesktopManager.CurrentDesktop);
                var closestNode = tree!.Root!.Windows
                    .OrderBy(x => Distance(center, x.ComputedRectangle.Center))
                    .FirstOrDefault();

                if (closestNode != null)
                {
                    return closestNode.WindowReference;
                }
            }

            return null;
        }
    }
}
