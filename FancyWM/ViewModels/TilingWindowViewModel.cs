using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

using WinMan;
using FancyWM.Layouts.Tiling;
using System.Linq;

namespace FancyWM.ViewModels
{
    public class TilingWindowViewModel : TilingNodeViewModel
    {
        private enum RevealState
        {
            /// <summary>
            /// Not enabled, mouse is far away
            /// </summary>
            Hidden,
            /// <summary>
            /// Disabled by mouse movement from the side.
            /// </summary>
            IndicatorVisible,
            /// <summary>
            /// Enabled
            /// </summary>
            Visible,
        }

        public string? Title { get => m_title; set => SetField(ref m_title, value); }

        public Visibility ActionsVisibility { get => m_actionsVisibility; set => SetField(ref m_actionsVisibility, value); }

        /// <summary>
        /// Bounds of the visible window in overlay coordinates. Updated on cursor movement.
        /// </summary>
        public Rectangle ActionsBounds { get => m_actionsBounds; set => SetField(ref m_actionsBounds, value); }

        public double ActionsHeight { get => m_actionsHeight; set => SetField(ref m_actionsHeight, value); }

        public double RevealHighlightRadius { get => m_revealHighlightRadius; set => SetField(ref m_revealHighlightRadius, value); }

        public double RevealHighlightOpacity { get => m_revealHighlightOpacity; set => SetField(ref m_revealHighlightOpacity, value); }

        public bool IsActionActive { get => m_isActionActive; set => SetField(ref m_isActionActive, value); }
        public bool IsPreviewVisible { get => m_isPreviewVisible; set => SetField(ref m_isPreviewVisible, value); }

        private IWorkspace? m_workspace;
        private string? m_title;
        private Visibility m_actionsVisibility = Visibility.Hidden;
        private Rectangle m_actionsBounds;
        private double m_actionsHeight = 22;
        private RevealState m_actionsRevealState = RevealState.Hidden;
        private double m_revealHighlightOpacity = 0;
        private double m_revealHighlightRadius = 64;
        private bool m_isMoving = false;
        private bool m_isPreviewVisible = false;
        private bool m_isActionActive = false;
        private WindowNode? m_currentNode;

        public event RoutedEventHandler? BeginHorizontalSplitWith;
        public event RoutedEventHandler? BeginVerticalSplitWith;
        public event RoutedEventHandler? BeginStackWith;

        public event RoutedEventHandler? FloatActionPressed;
        public event RoutedEventHandler? IgnoreProcessPressed;
        public event RoutedEventHandler? IgnoreClassPressed;

        public ICommand BeginHorizontalSplitWithCommand { get; }
        public ICommand BeginVerticalSplitWithCommand { get; }
        public ICommand BeginStackWithCommand { get; }

        public ICommand FloatCommand { get; }
        public ICommand IgnoreProcessCommand { get; }
        public ICommand IgnoreClassCommand { get; }

        public TilingWindowViewModel()
        {
            BeginHorizontalSplitWithCommand = new DelegateCommand(_ => BeginHorizontalSplitWith?.Invoke(this, new RoutedEventArgs()));
            BeginVerticalSplitWithCommand = new DelegateCommand(_ => BeginVerticalSplitWith?.Invoke(this, new RoutedEventArgs()));
            BeginStackWithCommand = new DelegateCommand(_ => BeginStackWith?.Invoke(this, new RoutedEventArgs()));

            FloatCommand = new DelegateCommand(_ => FloatActionPressed?.Invoke(this, new RoutedEventArgs()));
            IgnoreProcessCommand = new DelegateCommand(_ => IgnoreProcessPressed?.Invoke(this, new RoutedEventArgs()));
            IgnoreClassCommand = new DelegateCommand(_ => IgnoreClassPressed?.Invoke(this, new RoutedEventArgs()));
        }

        public override void Dispose()
        {
            base.Dispose();
            FloatActionPressed = null;
            IgnoreProcessPressed = null;
            IgnoreClassPressed = null;
            Node = null;
        }

        protected override void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            base.NotifyPropertyChanged(propertyName);
            if (propertyName == nameof(Node))
            {
                if (Node is WindowNode node)
                {
                    var workspace = node.WindowReference.Workspace;
                    if (m_workspace != workspace && m_workspace != null)
                    {
                        m_workspace.CursorLocationChanged -= OnCursorLocationChanged;
                    }
                    m_workspace = workspace;

                    if (workspace != null)
                    {
                        workspace.CursorLocationChanged += OnCursorLocationChanged;
                    }

                    if (m_currentNode != null)
                    {
                        m_currentNode.WindowReference.Removed -= WindowReference_Removed;
                        m_currentNode.WindowReference.PositionChangeStart -= WindowReference_PositionChangeStart;
                        m_currentNode.WindowReference.PositionChangeEnd -= WindowReference_PositionChangeEnd;
                        m_currentNode.WindowReference.TitleChanged -= WindowReference_TitleChanged;
                    }
                    m_currentNode = node;
                    node.WindowReference.Removed += WindowReference_Removed;
                    node.WindowReference.PositionChangeStart += WindowReference_PositionChangeStart;
                    node.WindowReference.PositionChangeEnd += WindowReference_PositionChangeEnd;
                    node.WindowReference.TitleChanged += WindowReference_TitleChanged;
                }
                else
                {
                    if (m_currentNode != null)
                    {
                        m_currentNode.WindowReference.Removed -= WindowReference_Removed;
                        m_currentNode.WindowReference.PositionChangeStart -= WindowReference_PositionChangeStart;
                        m_currentNode.WindowReference.PositionChangeEnd -= WindowReference_PositionChangeEnd;
                        m_currentNode.WindowReference.TitleChanged -= WindowReference_TitleChanged;
                    }
                    if (m_workspace != null)
                    {
                        m_workspace.CursorLocationChanged -= OnCursorLocationChanged;
                    }
                    m_workspace = null;
                    m_currentNode = null;
                }
            }
        }

        private void WindowReference_TitleChanged(object? sender, WindowTitleChangedEventArgs e)
        {
            Title = e.NewTitle;
        }

        private void WindowReference_Removed(object? sender, WindowChangedEventArgs e)
        {
            var window = (IWindow)sender!;
            window.Removed -= WindowReference_Removed;
            window.PositionChangeEnd -= WindowReference_PositionChangeEnd;
            window.PositionChangeStart -= WindowReference_PositionChangeStart;
            window.TitleChanged -= WindowReference_TitleChanged;
        }

        private void WindowReference_PositionChangeEnd(object? sender, WindowPositionChangedEventArgs e)
        {
            m_isMoving = false;
        }

        private void WindowReference_PositionChangeStart(object? sender, WindowPositionChangedEventArgs e)
        {
            m_isMoving = true;
        }

        private void OnCursorLocationChanged(object? sender, CursorLocationChangedEventArgs e)
        {
            if (Node is WindowNode node)
            {
                if (m_isActionActive)
                {
                    RevealHighlightOpacity = 0;
                    ActionsVisibility = Visibility.Visible;
                    m_actionsRevealState = RevealState.Visible;
                    return;
                }

                if (node.WindowReference.Workspace.FocusedWindow != node.WindowReference)
                {
                    RevealHighlightOpacity = 0;
                    ActionsVisibility = Visibility.Collapsed;
                    m_actionsRevealState = RevealState.Hidden;
                    return;
                }

                var windowPos = node.WindowReference.Position;

                // The window may be smaller than its cell (e.g. max size reached),
                // so the actions must follow the visible window, not the cell.
                var frame = node.WindowReference.FrameMargins;
                var displayOffsetX = ComputedBounds.Left - node.ComputedRectangle.Left;
                var displayOffsetY = ComputedBounds.Top - node.ComputedRectangle.Top;
                ActionsBounds = new Rectangle(
                    left: windowPos.Left + frame.Left + displayOffsetX,
                    top: windowPos.Top + frame.Top + displayOffsetY,
                    right: windowPos.Right - frame.Right + displayOffsetX,
                    bottom: windowPos.Bottom - frame.Bottom + displayOffsetY);

                var x = e.NewLocation.X - windowPos.Left;
                var y = e.NewLocation.Y - windowPos.Top;

                var isInBoundsX = 0 <= x && x <= windowPos.Width;

                var dpi = node.WindowReference.Workspace.DisplayManager.Displays.FirstOrDefault(x => x.Bounds.Contains(windowPos.Center))?.Scaling ?? 1.0;
                var revealHighlightRadius = RevealHighlightRadius * dpi;

                if (isInBoundsX && -(revealHighlightRadius / 2) < y && y <= 0)
                {
                    if (m_actionsRevealState == RevealState.IndicatorVisible)
                    {
                        m_actionsRevealState = RevealState.Visible;
                    }
                }
                else if (isInBoundsX && 0 <= y && y < revealHighlightRadius)
                {
                    if (m_actionsRevealState == RevealState.Hidden)
                    {
                        m_actionsRevealState = RevealState.IndicatorVisible;
                    }
                    RevealHighlightOpacity = 1 - Math.Pow(-y / revealHighlightRadius, 2);
                }
                else
                {
                    m_actionsRevealState = RevealState.Hidden;
                    RevealHighlightOpacity = 0;
                }

                if (m_actionsRevealState != RevealState.IndicatorVisible)
                {
                    RevealHighlightOpacity = 0;
                }

                ActionsVisibility = m_actionsRevealState == RevealState.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (m_isMoving)
                {
                    RevealHighlightOpacity = 0;
                    ActionsVisibility = Visibility.Collapsed;
                }
            }
        }
    }
}
