using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Threading;

using FancyWM.Models;
using FancyWM.Utilities;

using WinMan;
using FancyWM.Layouts.Tiling;
using Serilog;

namespace FancyWM
{
    class MultiDisplayTilingService : ITilingService, IDisposable
    {
        public event EventHandler<TilingFailedEventArgs>? PlacementFailed;
        public event EventHandler<EventArgs>? PendingIntentChanged;

        public Dispatcher Dispatcher { get; }
        public IWorkspace Workspace { get; }
        public IAnimationThread AnimationThread { get; }

        public bool Active => GetPrimaryTilingService().Active;

        public IReadOnlyCollection<IWindowMatcher> ExclusionMatchers
        {
            get => m_exclusionMatchers;
            set
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    tiling.ExclusionMatchers = value;
                }
                m_exclusionMatchers = value;
            }
        }

        public bool ShowFocus
        {
            get => m_showFocus;
            set
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    tiling.ShowFocus = value;
                }
                m_showFocus = value;
            }
        }

        public bool AutoCollapse
        {
            get => m_autoCollapse;
            set
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    tiling.AutoCollapse = value;
                }
                m_autoCollapse = value;
            }
        }

        public int AutoSplitCount
        {
            get => m_autoSplitCount;
            set
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    tiling.AutoSplitCount = value;
                }
                m_autoSplitCount = value;
            }
        }

        public ITilingServiceIntent? PendingIntent
        {
            get => GetActiveTilingService().PendingIntent;
            set => GetActiveTilingService().PendingIntent = value;
        }

        private readonly Dictionary<IDisplay, TilingService> m_tilingServices = [];
        private readonly CompositeDisposable m_subscriptions = [];
        private readonly Subject<Unit> m_focusedWindowLocationChanges = new();
        private readonly ILogger m_logger;
        private IDisplay m_activeDisplay;
        private bool m_showFocus;
        private bool m_autoCollapse;
        private int m_autoSplitCount;
        private IReadOnlyCollection<IWindowMatcher> m_exclusionMatchers = [];
        private readonly IObservable<ITilingServiceSettings> m_settings;
        private readonly object m_syncRoot = new();

        public MultiDisplayTilingService(IWorkspace workspace, IAnimationThread animationThread, IObservable<ITilingServiceSettings> settings)
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            Workspace = workspace;
            AnimationThread = animationThread;
            m_settings = settings;
            m_logger = App.Current.Logger;
            m_logger.Warning($"Using the multi-monitor tiling backend");

            foreach (var display in Workspace.DisplayManager.Displays)
            {
                m_logger.Debug($"Managing display {display}");
                var tiling = new TilingService(Workspace, display, animationThread, settings, true)
                {
                    ExclusionMatchers = m_exclusionMatchers,
                    ShowFocus = m_showFocus,
                };
                tiling.PlacementFailed += OnTilingFailed;
                tiling.Start();
                m_tilingServices.Add(display, tiling);
            }

            m_activeDisplay = GetActiveDisplay() ?? Workspace.DisplayManager.PrimaryDisplay;
            m_tilingServices[m_activeDisplay].AutoRegisterWindows = true;
            m_tilingServices[m_activeDisplay].Refresh();

            Workspace.DisplayManager.Added += OnDisplayAdded;
            Workspace.DisplayManager.Removed += OnDisplayRemoved;

            Workspace.FocusedWindowChanged += OnFocusedWindowChanged;
            m_subscriptions.Add(Disposable.Create(() => Workspace.FocusedWindowChanged -= OnFocusedWindowChanged));

            var focusLocationObservable = m_focusedWindowLocationChanges
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Do(async _ => await Dispatcher.InvokeAsync(() =>
                {
                    UpdateActiveDisplay(reason: "the focused window was moved");
                }));
            m_subscriptions.Add(focusLocationObservable.Subscribe());
        }

        private void OnDisplayRemoved(object? sender, DisplayChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                lock (m_syncRoot)
                {
                    m_logger.Debug($"Removed display {e.Source}");
                    if (m_activeDisplay.Equals(e.Source))
                    {
                        UpdateActiveDisplay($"the active display {e.Source} was removed");
                    }
                    if (m_tilingServices.TryGetValue(e.Source, out var tiling))
                    {
                        m_tilingServices.Remove(e.Source);
                        tiling.PlacementFailed -= OnTilingFailed;
                        tiling.PendingIntentChanged -= OnPendingIntentChanged;
                        tiling.Stop();
                        tiling.Dispose();
                    }
                }

                GCHelper.ScheduleCollection();
            });
        }

        private void OnDisplayAdded(object? sender, DisplayChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                lock (m_syncRoot)
                {
                    m_logger.Debug($"Added display {e.Source}");
                    var tiling = new TilingService(Workspace, e.Source, AnimationThread, m_settings, true)
                    {
                        ShowFocus = m_showFocus,
                        ExclusionMatchers = m_exclusionMatchers,
                    };
                    tiling.PlacementFailed += OnTilingFailed;
                    tiling.PendingIntentChanged += OnPendingIntentChanged;
                    tiling.Start();
                    m_tilingServices.Add(e.Source, tiling);

                    UpdateActiveDisplay(reason: $"display {e.Source} was added");
                }
            });
        }

        private void OnPendingIntentChanged(object? sender, EventArgs e)
        {
            PendingIntentChanged?.Invoke(this, e);
            foreach (var tiling in m_tilingServices)
            {
                tiling.Value.PendingIntent = ((TilingService)sender!).PendingIntent;
            }
        }

        private void OnWindowPositionChanged(object? sender, WindowPositionChangedEventArgs e)
        {
            m_focusedWindowLocationChanges.OnNext(Unit.Default);
        }

        private void OnFocusedWindowChanged(object? sender, FocusedWindowChangedEventArgs e)
        {
            if (e.OldFocusedWindow != null)
            {
                e.OldFocusedWindow.PositionChanged -= OnWindowPositionChanged;
            }
            if (e.NewFocusedWindow == null)
            {
                return;
            }
            e.NewFocusedWindow.PositionChanged += OnWindowPositionChanged;

            _ = Dispatcher.InvokeAsync(() =>
            {
                UpdateActiveDisplay($"the focused window has changed to {e.NewFocusedWindow.Handle}={e.NewFocusedWindow.GetCachedProcessName()}");
            });
        }

        private void UpdateActiveDisplay(string? reason = null)
        {
            lock (m_syncRoot)
            {
                var newActiveDisplay = GetActiveDisplay() ?? Workspace.DisplayManager.PrimaryDisplay;
                if (!newActiveDisplay.Equals(m_activeDisplay))
                {
                    m_logger.Information($"Active display changed from {m_activeDisplay} to {newActiveDisplay}");
                    if (m_tilingServices.TryGetValue(m_activeDisplay, out var oldTiling))
                    {
                        oldTiling.AutoRegisterWindows = true;
                    }
                    else
                    {
                        m_logger.Warning($"Previous active display {m_activeDisplay} is not associated with a tiling service!");
                    }
                    m_activeDisplay = newActiveDisplay;
                    if (m_tilingServices.TryGetValue(m_activeDisplay, out var newTiling))
                    {
                        newTiling.AutoRegisterWindows = true;
                        newTiling.Refresh();
                    }
                    else
                    {
                        m_logger.Warning($"New active display {m_activeDisplay} is not associated with a tiling service!");
                    }
                    m_logger.Verbose($"Check triggered because {reason}.");
                }
            }
        }

        private IDisplay? GetActiveDisplay()
        {
            lock (m_syncRoot)
            {
                var window = Workspace.FocusedWindow;
                if (window == null)
                    return null;
                var display = Workspace.DisplayManager.Displays
                    .FirstOrDefault(x => x.Bounds.Contains(window.Position.Center));
                return display;
            }
        }

        private TilingService GetActiveTilingService()
        {
            lock (m_syncRoot)
            {
                return m_tilingServices[m_activeDisplay];
            }
        }

        private TilingService GetPrimaryTilingService()
        {
            lock (m_syncRoot)
            {
                return m_tilingServices[Workspace.DisplayManager.PrimaryDisplay];
            }
        }

        private void OnTilingFailed(object? sender, TilingFailedEventArgs e)
        {
            PlacementFailed?.Invoke(this, e);
        }

        public void Dispose()
        {
            lock (m_syncRoot)
            {
                m_subscriptions.Dispose();
            }
        }

        public void Stack()
        {
            GetActiveTilingService().Stack();
        }

        public bool DiscoverWindows()
        {
            bool anyChanges = false;
            lock (m_syncRoot)
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    anyChanges = anyChanges || tiling.DiscoverWindows();
                }
            }
            return anyChanges;
        }

        public void Refresh()
        {
            lock (m_syncRoot)
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    tiling.Refresh();
                }
            }
        }

        public void Float()
        {
            GetActiveTilingService().Float();
        }

        public void MoveFocus(TilingDirection direction)
        {
            var tiling = GetActiveTilingService();
            if (tiling.CanMoveFocus(direction))
            {
                tiling.MoveFocus(direction);
                return;
            }

            var closest = m_tilingServices.Values
                .Where(x => x != tiling)
                .OrderBy(x => SqrDistanceInDirection(tiling.GetBounds().Center, x.GetBounds().Center, direction))
                .FirstOrDefault();
            if (closest == null)
            {
                return;
            }

            IWindow? focusedWindow = tiling.GetFocus();
            if (focusedWindow == null)
            {
                return;
            }

            var closestWindow = closest.FindClosest(focusedWindow.Position.Center);
            if (closestWindow != null)
            {
                FocusHelper.ForceActivate(closestWindow.Handle);
            }
        }

        public void PullUp()
        {
            GetActiveTilingService().PullUp();
        }

        public void SwapFocus(TilingDirection direction)
        {
            GetActiveTilingService().SwapFocus(direction);
        }

        public void Split(bool vertical)
        {
            GetActiveTilingService().Split(vertical);
        }

        public void Stop()
        {
            lock (m_syncRoot)
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    tiling.Stop();
                }
            }
        }

        public void Start()
        {
            lock (m_syncRoot)
            {
                foreach (var tiling in m_tilingServices.Values)
                {
                    tiling.Start();
                }
            }
        }

        public bool CanSplit(bool vertical)
        {
            return GetActiveTilingService().CanSplit(vertical);
        }

        public bool CanStack()
        {
            return GetActiveTilingService().CanStack();
        }

        public bool CanFloat()
        {
            return GetActiveTilingService().CanFloat();
        }

        public bool CanMoveFocus(TilingDirection direction)
        {
            if (GetActiveTilingService().CanMoveFocus(direction))
            {
                return true;
            }

            var tiling = GetActiveTilingService();
            var closest = m_tilingServices.Values
                .OrderBy(x => SqrDistanceInDirection(tiling.GetBounds().Center, x.GetBounds().Center, direction))
                .FirstOrDefault();
            if (closest == null)
            {
                return false;
            }

            IWindow? focusedWindow = tiling.GetFocus();
            if (focusedWindow == null)
            {
                return false;
            }

            return closest.FindClosest(focusedWindow.Position.Center) != null;
        }

        public bool CanPullUp()
        {
            return GetActiveTilingService().CanPullUp();
        }

        public bool CanSwapFocus(TilingDirection direction)
        {
            return GetActiveTilingService().CanMoveFocus(direction);
        }

        public bool CanMoveWindow(TilingDirection direction)
        {
            return GetActiveTilingService().CanMoveWindow(direction);
        }

        public void MoveWindow(TilingDirection direction)
        {
            GetActiveTilingService().MoveWindow(direction);
        }

        public bool CanResize(PanelOrientation orientation, double displayPercentage)
        {
            return GetActiveTilingService().CanResize(orientation, displayPercentage);
        }

        public void Resize(PanelOrientation orientation, double displayPercentage)
        {
            GetActiveTilingService().Resize(orientation, displayPercentage);
        }

        public void ToggleDesktop()
        {
            GetActiveTilingService().ToggleDesktop();
        }

        public IWindow? GetFocus()
        {
            return GetActiveTilingService().GetFocus();
        }

        public Rectangle GetBounds()
        {
            throw new NotSupportedException();
        }

        public IWindow? FindClosest(Point center)
        {
            throw new NotSupportedException();
        }

        private static double SqrDistanceInDirection(Point from, Point to, TilingDirection direction)
        {
            switch (direction)
            {
                case TilingDirection.Left:
                    if (from.X > to.X)
                        return double.PositiveInfinity;
                    break;
                case TilingDirection.Right:
                    if (from.X < to.X)
                        return double.PositiveInfinity;
                    break;
                case TilingDirection.Up:
                    if (from.Y < to.Y)
                        return double.PositiveInfinity;
                    break;
                case TilingDirection.Down:
                    if (from.Y > to.Y)
                        return double.PositiveInfinity;
                    break;
            }

            return (from.X - to.X) * (from.X - to.X) + (from.Y - to.Y) * (from.Y - to.Y);
        }
    }
}
