using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace FancyWM.Utilities
{
    internal class Draggable
    {
        private class DragData
        {
            public Point InitialPosition { get; internal set; }
            public bool WindowBackgroundRequiredFixing { get; internal set; }
        }

        public static readonly RoutedEvent DragCompletedEvent = EventManager.RegisterRoutedEvent(
            "DragCompleted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FrameworkElement));

        public static void AddDragCompletedHandler(UIElement element, RoutedEventHandler handler)
        {
            element.AddHandler(DragCompletedEvent, handler);
        }

        public static void RemoveDragCompletedHandler(UIElement element, RoutedEventHandler handler)
        {
            element.RemoveHandler(DragCompletedEvent, handler);
        }

        public static readonly RoutedEvent DragStartedEvent = EventManager.RegisterRoutedEvent(
            "DragStarted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FrameworkElement));

        public static void AddDragStartedHandler(UIElement element, RoutedEventHandler handler)
        {
            element.AddHandler(DragStartedEvent, handler);
        }

        public static void RemoveDragStartedHandler(UIElement element, RoutedEventHandler handler)
        {
            element.RemoveHandler(DragStartedEvent, handler);
        }

        public static readonly DependencyProperty IsDraggableProperty = DependencyProperty.RegisterAttached(
            "IsDraggable",
            typeof(bool),
            typeof(Draggable),
            new PropertyMetadata(false, OnIsDraggableChanged)
        );

        public static void SetIsDraggable(UIElement element, bool value)
        {
            element.SetValue(IsDraggableProperty, value);
        }

        public static bool GetIsDraggable(UIElement element)
        {
            return (bool)element.GetValue(IsDraggableProperty);
        }

        private static void OnIsDraggableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element))
            {
                throw new ArgumentException("IsDraggable is only valid on FrameworkElements!");
            }

            if ((bool)e.NewValue)
            {
                InitDraggable(element);
            }
            else
            {
                UninitDraggable(element);
            }
        }

        private static void InitDraggable(UIElement element)
        {
            element.MouseDown += OnElementMouseDown;
            element.MouseMove += OnElementMouseMove;
            element.MouseUp += OnElementMouseUp;
            element.LostMouseCapture += OnElementMouseCaptureLost;
        }

        private static void UninitDraggable(UIElement element)
        {
            element.MouseDown -= OnElementMouseDown;
            element.MouseMove -= OnElementMouseMove;
            element.MouseUp -= OnElementMouseUp;
            element.LostMouseCapture -= OnElementMouseCaptureLost;
        }

        private static readonly ConditionalWeakTable<UIElement, DragData> s_dragData = new ConditionalWeakTable<UIElement, DragData>();

        private static void BeginDrag(FrameworkElement element, Point mousePosition)
        {
            if (element.CaptureMouse())
            {
                element.RenderTransform = new TranslateTransform();
                bool requiredFixing = TryFixWindowBackground(element);
                s_dragData.AddOrUpdate(element, new DragData { InitialPosition = mousePosition, WindowBackgroundRequiredFixing = requiredFixing });
                element.RaiseEvent(new RoutedEventArgs(DragStartedEvent, element));
            }
        }

        private static void UpdateDrag(FrameworkElement element, Point mousePosition)
        {
            if (s_dragData.TryGetValue(element, out DragData? data))
            {
                var offset = mousePosition - data.InitialPosition;
                if (element.RenderTransform is TranslateTransform transform)
                {
                    transform.X = offset.X;
                    transform.Y = offset.Y;
                }
                else
                {
                    throw new ArgumentException("IsDraggable cannot be used on elements with RenderTransform specified!");
                }
            }
        }

        private static void CancelDrag(FrameworkElement element)
        {
            if (s_dragData.TryGetValue(element, out DragData? data))
            {
                if (element.RenderTransform is TranslateTransform || element.RenderTransform == null)
                {
                    s_dragData.Remove(element);
                    element.ReleaseMouseCapture();
                    element.RaiseEvent(new RoutedEventArgs(DragCompletedEvent, element));
                    element.Dispatcher.InvokeAsync(() =>
                    {
                        element.RenderTransform = null;
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    throw new ArgumentException("IsDraggable cannot be used on elements with RenderTransform specified!");
                }

                if (data.WindowBackgroundRequiredFixing)
                {
                    UnfixWindowBackground(element);
                }
            }
        }

        private static bool TryFixWindowBackground(FrameworkElement element)
        {
            var window = Window.GetWindow(element);
            if (window.Background == null)
            {
                window.Background = Brushes.Transparent;
                return true;
            }
            return false;
        }

        private static void UnfixWindowBackground(FrameworkElement element)
        {
            var window = Window.GetWindow(element);
            if (window.Background == Brushes.Transparent)
            {
                window.Background = null;
            }
        }

        private static Point GetMousePosition(FrameworkElement element)
        {
            var window = Window.GetWindow(element);
            return window.PointToScreen(Mouse.GetPosition(window));
        }

        private static void OnElementMouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = (FrameworkElement)sender;
            if (e.ChangedButton == MouseButton.Left)
            {
                BeginDrag(element, GetMousePosition(element));
                e.Handled = true;
            }
        }

        private static void OnElementMouseMove(object sender, MouseEventArgs e)
        {
            // TODO: Null referecne here ?
            var element = (FrameworkElement)sender;
            UpdateDrag(element, GetMousePosition(element));
        }

        private static void OnElementMouseUp(object sender, MouseButtonEventArgs e)
        {
            var element = (FrameworkElement)sender;
            if (e.ChangedButton == MouseButton.Left)
            {
                CancelDrag(element);
                e.Handled = true;
            }
        }

        private static void OnElementMouseCaptureLost(object sender, MouseEventArgs e)
        {
            var element = (FrameworkElement)sender;
            CancelDrag(element);
        }
    }
}
