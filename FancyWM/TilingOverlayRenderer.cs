using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

using FancyWM.Layouts.Tiling;
using FancyWM.Utilities;
using FancyWM.ViewModels;
using FancyWM.Controls;
using WinMan;
using FancyWM.Windows;

namespace FancyWM
{
    public class TilingOverlayRenderer : IDisposable
    {
        public event EventHandler<PanelNode>? TilingPanelMoveRequested;
        public event EventHandler<TilingNode>? TilingNodeFocusRequested;
        public event EventHandler<TilingNode>? TilingNodePullUpRequested;
        public event EventHandler<TilingNode>? TilingNodeCloseRequested;


        public event EventHandler<TilingNode>? HorizontalSplitRequested;
        public event EventHandler<TilingNode>? VerticalSplitRequested;
        public event EventHandler<TilingNode>? StackRequested;
        public event EventHandler<TilingNode>? PullUpRequested;
        public event EventHandler<WindowNode>? FloatRequested;
        public event EventHandler<WindowNode>? IgnoreProcessRequested;
        public event EventHandler<WindowNode>? IgnoreClassRequested;
        public event EventHandler<WindowNode>? BeginHorizontalWithRequested;
        public event EventHandler<WindowNode>? BeginVerticalWithRequested;
        public event EventHandler<WindowNode>? BeginStackWithRequested;

        public int PanelSpacing { get; set; }
        public Thickness PanelPadding { get; set; }

        public IReadOnlySet<IWindow> PreviewWindows
        {
            get => m_previewWindows;
            set
            {
                if (m_previewWindows != value)
                {
                    OnSetPreviewWindows(oldValue: m_previewWindows, newValue: value);
                    m_previewWindows = value;
                }
            }
        }

        public IWindow? IntentSourceWindow
        {
            get => m_intentSourceWindow;
            set
            {
                if (m_intentSourceWindow != value)
                {
                    OnSetIntentSourceWindow(oldValue: m_intentSourceWindow, newValue: value);
                    m_intentSourceWindow = value;
                }
            }
        }

        private readonly OverlayHost m_overlay;
        private readonly DelegateCommand<TilingNodeViewModel> m_panelItemPrimaryActionCommand;
        private readonly DelegateCommand<TilingNodeViewModel> m_panelItemSecondaryActionCommand;
        private readonly DelegateCommand<TilingNodeViewModel> m_panelItemCloseActionCommand;
        private readonly IDisplay m_display;
        private bool m_isOverlayInit = false;
        private TilingOverlayViewModel m_viewModel = new();

        private IReadOnlyCollection<TilingNode> m_previousSnapshot = Array.Empty<TilingNode>();
        private IDictionary<TilingNode, TilingNodeViewModel> m_nodeViewModels = new Dictionary<TilingNode, TilingNodeViewModel>();
        private IReadOnlySet<IWindow> m_previewWindows = new HashSet<IWindow>();
        private IWindow? m_intentSourceWindow;

        public TilingOverlayRenderer(IDisplay display, Func<IntPtr> overlayAnchorSource)
        {
            m_overlay = new OverlayHost(display);
            m_overlay.AnchorSource = overlayAnchorSource;
            m_overlay.Show();

            m_panelItemPrimaryActionCommand = new DelegateCommand<TilingNodeViewModel>(OnOverlayPanelItemClick);
            m_panelItemSecondaryActionCommand = new DelegateCommand<TilingNodeViewModel>(OnOverlayPanelItemSecondaryAction);
            m_panelItemCloseActionCommand = new DelegateCommand<TilingNodeViewModel>(OnOverlayPanelItemCloseAction);
            m_display = display;
        }

        public void UpdateOverlay(IReadOnlyCollection<TilingNode> snapshot, IReadOnlyCollection<TilingNode> focusedPath)
        {
            UpdateViewModels(snapshot, focusedPath);

            var focusedWindow = focusedPath.FirstOrDefault() as FancyWM.Layouts.Tiling.WindowNode;
            if (!m_isOverlayInit)
            {
                m_isOverlayInit = true;

                var overlayView = new TilingOverlay
                {
                    ViewModel = m_viewModel
                };
                Draggable.AddDragStartedHandler(overlayView, OnDragStarted);
                Draggable.AddDragCompletedHandler(overlayView, OnDragCompleted);
                m_overlay.Content = overlayView;

                m_overlay.NonHitTestableContent = new NonHitTestableTilingOverlay
                {
                    ViewModel = m_viewModel
                };
            }
        }

        private Rectangle AdjustForDisplay(Rectangle rectangle)
        {
            var bounds = m_display.Bounds;
            return new Rectangle(rectangle.Left - bounds.Left, rectangle.Top - bounds.Top, rectangle.Right - bounds.Left, rectangle.Bottom - bounds.Top);
        }

        private void UpdateViewModels(IReadOnlyCollection<TilingNode> snapshot, IReadOnlyCollection<TilingNode> focusedPath)
        {
            (var addList, var removeList, var persistList) = m_previousSnapshot.Changes(snapshot);
            m_previousSnapshot = snapshot;

            foreach (var removedNode in removeList)
            {
                if (m_nodeViewModels.TryGetValue(removedNode, out var vm))
                {
                    m_nodeViewModels.Remove(removedNode);
                    switch (vm)
                    {
                        case TilingPanelViewModel panelViewModel:
                            m_viewModel.PanelElements.Remove(panelViewModel);
                            break;
                        case TilingWindowViewModel windowViewModel:
                            m_viewModel.WindowElements.Remove(windowViewModel);
                            break;
                        default:
                            continue;
                    }
                    vm.Dispose();
                }
            }

            foreach (var addedNode in addList)
            {
                var vm = CreateViewModel(addedNode, focusedPath);
                switch (vm)
                {
                    case TilingPanelViewModel panelViewModel:
                        m_viewModel.PanelElements.Add(panelViewModel);
                        break;
                    case TilingWindowViewModel windowViewModel:
                        m_viewModel.WindowElements.Add(windowViewModel);
                        break;
                    default:
                        continue;
                }
            }

            foreach (var persistedNode in persistList.Concat(addList))
            {
                var vm = GetViewModel(persistedNode, focusedPath);
                switch (vm)
                {
                    case TilingPanelViewModel panelViewModel:
                        UpdateViewModel(panelViewModel, (PanelNode)persistedNode, focusedPath);
                        break;
                    case TilingWindowViewModel windowViewModel:
                        UpdateViewModel(windowViewModel, (WindowNode)persistedNode, focusedPath);
                        break;
                    default:
                        continue;
                }
            }
        }

        private TilingNodeViewModel? GetViewModel(TilingNode node, IEnumerable<TilingNode> focusedPath)
        {
            if (m_nodeViewModels.TryGetValue(node, out var vm))
            {
                return vm;
            }
            return null;
        }

        private TilingNodeViewModel? CreateViewModel(TilingNode node, IEnumerable<TilingNode> focusedPath)
        {
            switch (node)
            {
                case PanelNode panelNode:
                    var panelViewModel = new TilingPanelViewModel();
                    panelViewModel.HorizontalSplitActionPressed += WindowViewModel_HorizontalSplitActionPressed;
                    panelViewModel.VerticalSplitActionPressed += WindowViewModel_VerticalSplitActionPressed;
                    panelViewModel.PullUpActionPressed += WindowViewModel_PullUpActionPressed;
                    panelViewModel.StackActionPressed += WindowViewModel_StackActionPressed;
                    UpdateViewModel(panelViewModel, panelNode, focusedPath);
                    m_nodeViewModels.Add(node, panelViewModel);
                    return panelViewModel;
                case WindowNode windowNode:
                    var windowViewModel = new TilingWindowViewModel();
                    windowViewModel.BeginHorizontalSplitWith += WindowViewModel_BeginHorizontalSplitWith;
                    windowViewModel.BeginVerticalSplitWith += WindowViewModel_BeginVerticalSplitWith;
                    windowViewModel.BeginStackWith += WindowViewModel_BeginStackWith;
                    windowViewModel.FloatActionPressed += WindowViewModel_FloatActionPressed;
                    windowViewModel.HorizontalSplitActionPressed += WindowViewModel_HorizontalSplitActionPressed;
                    windowViewModel.VerticalSplitActionPressed += WindowViewModel_VerticalSplitActionPressed;
                    windowViewModel.PullUpActionPressed += WindowViewModel_PullUpActionPressed;
                    windowViewModel.StackActionPressed += WindowViewModel_StackActionPressed;
                    windowViewModel.IgnoreClassPressed += WindowViewModel_IgnoreClassPressed;
                    windowViewModel.IgnoreProcessPressed += WindowViewModel_IgnoreProcessPressed;
                    UpdateViewModel(windowViewModel, windowNode, focusedPath);
                    m_nodeViewModels.Add(node, windowViewModel);
                    return windowViewModel;
                default:
                    return null;
            }
        }

        private void WindowViewModel_BeginHorizontalSplitWith(object sender, RoutedEventArgs e)
        {
            BeginHorizontalWithRequested?.Invoke(this, (WindowNode)((TilingWindowViewModel)sender!).Node!);
        }

        private void WindowViewModel_BeginVerticalSplitWith(object sender, RoutedEventArgs e)
        {
            BeginVerticalWithRequested?.Invoke(this, (WindowNode)((TilingWindowViewModel)sender!).Node!);
        }

        private void WindowViewModel_BeginStackWith(object sender, RoutedEventArgs e)
        {
            BeginStackWithRequested?.Invoke(this, (WindowNode)((TilingWindowViewModel)sender!).Node!);
        }

        internal void Show()
        {
            m_overlay.Show();
        }

        internal void Hide()
        {
            m_overlay.Hide();
        }

        private void WindowViewModel_StackActionPressed(object sender, RoutedEventArgs e)
        {
            StackRequested?.Invoke(this, ((TilingNodeViewModel)sender!).Node!);
        }

        private void WindowViewModel_PullUpActionPressed(object sender, RoutedEventArgs e)
        {
            PullUpRequested?.Invoke(this, ((TilingNodeViewModel)sender!).Node!);
        }

        private void WindowViewModel_VerticalSplitActionPressed(object sender, RoutedEventArgs e)
        {
            VerticalSplitRequested?.Invoke(this, ((TilingNodeViewModel)sender!).Node!);
        }

        private void WindowViewModel_HorizontalSplitActionPressed(object sender, RoutedEventArgs e)
        {
            HorizontalSplitRequested?.Invoke(this, ((TilingNodeViewModel)sender!).Node!);
        }

        private void WindowViewModel_FloatActionPressed(object sender, RoutedEventArgs e)
        {
            FloatRequested?.Invoke(this, (WindowNode)((TilingWindowViewModel)sender!).Node!);
        }

        private void WindowViewModel_IgnoreProcessPressed(object sender, RoutedEventArgs e)
        {
            IgnoreProcessRequested?.Invoke(this, ((WindowNode)((TilingWindowViewModel)sender!).Node!));
        }

        private void WindowViewModel_IgnoreClassPressed(object sender, RoutedEventArgs e)
        {
            IgnoreClassRequested?.Invoke(this, ((WindowNode)((TilingWindowViewModel)sender!).Node!));
        }

        private void OnSetPreviewWindows(IReadOnlySet<IWindow> oldValue, IReadOnlySet<IWindow> newValue)
        {
            foreach (var oldVm in m_nodeViewModels.Where(x => x.Key is WindowNode window && oldValue.Contains(window.WindowReference))
                .Select(x => x.Value)
                .OfType<TilingWindowViewModel>())
            {
                oldVm.IsPreviewVisible = false;
            }
            foreach (var newVm in m_nodeViewModels.Where(x => x.Key is WindowNode window && newValue.Contains(window.WindowReference))
                .Select(x => x.Value)
                .OfType<TilingWindowViewModel>())
            {
                newVm.IsPreviewVisible = true;
            }
        }

        private void OnSetIntentSourceWindow(IWindow? oldValue, IWindow? newValue)
        {
            //if (m_nodeViewModels.FirstOrDefault(x => x.Key is WindowNode window && window.WindowReference == oldValue) is WindowNode oldVm)
            //{
            //    oldVm.IsIntentSource = true;
            //}

            //foreach (var oldVm in m_nodeViewModels.Where(x => x.Key is WindowNode window && oldValue.Contains(window.WindowReference))
            //    .Select(x => x.Value)
            //    .OfType<TilingWindowViewModel>())
            //{
            //    oldVm.IsPreviewVisible = false;
            //}
            //foreach (var newVm in m_nodeViewModels.Where(x => x.Key is WindowNode window && newValue.Contains(window.WindowReference))
            //    .Select(x => x.Value)
            //    .OfType<TilingWindowViewModel>())
            //{
            //    newVm.IsPreviewVisible = true;
            //}
        }


        private void UpdateViewModel(TilingWindowViewModel vm, WindowNode node, IEnumerable<TilingNode> focusedPath)
        {
            vm.Node = node;
            vm.Title = node.WindowReference.Title;
            vm.HasFocus = focusedPath.Contains(node);
            vm.ComputedBounds = AdjustForDisplay(node.ComputedRectangle);
            vm.PrimaryActionCommand = m_panelItemPrimaryActionCommand;
            vm.SecondaryActionCommand = m_panelItemSecondaryActionCommand;
            vm.CloseCommand = m_panelItemCloseActionCommand;
        }

        private void UpdateViewModel(TilingPanelViewModel vm, PanelNode node, IEnumerable<TilingNode> focusedPath)
        {
            vm.Node = node;
            vm.HasFocus = focusedPath.Contains(node);
            vm.ChildHasDirectFocus = vm.ChildNodes.Select(x => x.Node).Contains(focusedPath.FirstOrDefault());
            vm.ComputedBounds = AdjustForDisplay(node.ComputedRectangle);
            vm.PrimaryActionCommand = m_panelItemPrimaryActionCommand;
            vm.SecondaryActionCommand = m_panelItemSecondaryActionCommand;
            vm.CloseCommand = m_panelItemCloseActionCommand;

            vm.HeaderBounds = Rectangle.OffsetAndSize(
                (int)(vm.ComputedBounds.Left - node.Padding.Left + PanelSpacing / 2),
                (int)(vm.ComputedBounds.Top - node.Padding.Top + PanelSpacing / 2),
                (int)(vm.ComputedBounds.Width - PanelSpacing),
                (int)(PanelPadding.Top - PanelSpacing));

            vm.IsHeaderVisible = !IsObscured(node, focusedPath);

            if (HaveChildrenChanged(vm, node, focusedPath))
            {
                vm.ChildNodes.Clear();
                foreach (var child in node.Children)
                {
                    var childViewModel = GetViewModel(child, focusedPath);
                    if (childViewModel == null)
                    {
                        continue;
                    }
                    vm.ChildNodes.Add(childViewModel);
                }
            }
        }

        private bool HaveChildrenChanged(TilingPanelViewModel vm, PanelNode node, IEnumerable<TilingNode> focusedPath)
        {
            if (vm.ChildNodes.Count != node.Children.Count)
            {
                return true;
            }
            int i = 0;
            foreach (var child in node.Children)
            {
                var childViewModel = GetViewModel(child, focusedPath);
                if (childViewModel == null)
                {
                    continue;
                }
                if (childViewModel != vm.ChildNodes[i])
                {
                    return true;
                }
                i++;
            }
            return false;
        }

        private bool IsObscured(PanelNode node, IEnumerable<TilingNode> focusedPath)
        {
            if (focusedPath.Contains(node))
                return false;

            var stackAncestors = node.Ancestors
                .OfType<StackPanelNode>();

            if (!stackAncestors.Any())
                return false;

            return stackAncestors
                .All(x => focusedPath.Contains(x));
        }

        private void OnOverlayPanelItemClick(TilingNodeViewModel viewModel)
        {
            TilingNodeFocusRequested?.Invoke(this, viewModel.Node!);
        }

        private void OnOverlayPanelItemSecondaryAction(TilingNodeViewModel viewModel)
        {
            TilingNodePullUpRequested?.Invoke(this, viewModel.Node!);
        }

        private void OnOverlayPanelItemCloseAction(TilingNodeViewModel viewModel)
        {
            TilingNodeCloseRequested?.Invoke(this, viewModel.Node!);
        }

        private void OnDragStarted(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TilingPanel view)
            {
                if (view.ViewModel != null)
                {
                    view.ViewModel.IsMoving = true;
                }
            }
        }

        private void OnDragCompleted(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TilingPanel view)
            {
                if (view.ViewModel != null)
                {
                    view.ViewModel.IsMoving = false;
                    TilingPanelMoveRequested?.Invoke(this, (PanelNode)view.ViewModel.Node!);
                }
            }
        }

        public void InvalidateView()
        {
            m_viewModel.PanelElements.Clear();
            m_viewModel.WindowElements.Clear();
            foreach (var vm in m_nodeViewModels)
            {
                vm.Value.Dispose();
            }
            m_nodeViewModels.Clear();
            m_previousSnapshot = Array.Empty<TilingNode>();
        }

        public void Dispose()
        {
            if (m_overlay.Content != null)
            {
                Draggable.RemoveDragStartedHandler(m_overlay.Content, OnDragStarted);
                Draggable.RemoveDragCompletedHandler(m_overlay.Content, OnDragCompleted);
            }
            m_overlay.Close();
            m_viewModel.Dispose();
            InvalidateView();

            TilingPanelMoveRequested = null;
            TilingNodeFocusRequested = null;
            TilingNodeCloseRequested = null;
            TilingNodePullUpRequested = null;

            HorizontalSplitRequested = null;
            VerticalSplitRequested = null;
            StackRequested = null;
            PullUpRequested = null;
            FloatRequested = null;
            IgnoreProcessRequested = null;
            IgnoreClassRequested = null;
        }
    }
}
