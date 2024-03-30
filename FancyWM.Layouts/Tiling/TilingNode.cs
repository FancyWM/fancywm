using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public enum TilingNodeType
    {
        Split,
        Stack,
        Static,
        Window,
        Placeholder,
    }

    public abstract class TilingNode : ICloneable
    {
        private static long s_generationId = 0;

        public long GenerationID { get; } = Interlocked.Increment(ref s_generationId);

        public DesktopTree? Desktop
        {
            get
            {
                if (Parent != null)
                {
                    return Parent.Desktop;
                }
                return m_desktop;
            }
            internal set
            {
                m_desktop = value;
            }
        }

        public PanelNode? Parent { get; internal set; }

        public abstract IEnumerable<WindowNode> Windows { get; }

        public abstract TilingNodeType Type { get; }

        public IEnumerable<TilingNode> Ancestors
        {
            get
            {
                var p = Parent;
                while (p != null)
                {
                    yield return p;
                    p = p.Parent;
                }
            }
        }

        public IEnumerable<TilingNode> PathToRoot
        {
            get
            {
                yield return this;
                foreach (var node in Ancestors)
                {
                    yield return node;
                }
            }
        }

        public abstract IEnumerable<TilingNode> Nodes { get; }

        public Point MinSize { get; protected set; }

        public Point MaxSize { get; protected set; } = new Point(short.MaxValue, short.MaxValue);

        public Rectangle Padding { get; set; }

        public Rectangle ComputedRectangle => Rectangle.ToRectangle();

        internal RectangleF Rectangle { get; private set; }

        private DesktopTree? m_desktop;

        internal TilingNode() { }

        public void Swap(TilingNode otherNode)
        {
            DesktopTree.SwapReferences(this, otherNode);
        }

        public void Remove(bool cleanup = false, bool collapse = false)
        {
            if (Desktop == null)
            {
                throw new InvalidOperationException($"Node is not attached to Desktop!");
            }

            var oldParent = Parent!;
            oldParent.Detach(this);
            if (cleanup)
            {
                oldParent.Cleanup(collapse);
            }
        }

        private TilingNode? FindAdjacentY(int direction)
        {
            if (Parent is SplitPanelNode sp && sp.Orientation == PanelOrientation.Vertical)
            {
                int index = sp.IndexOf(this);
                int nextIndex = index + direction;
                if (sp.Children.Count == nextIndex || nextIndex == -1)
                {
                    return sp.FindAdjacentY(direction);
                }
                return sp.Children[nextIndex];
            }
            return Parent?.FindAdjacentY(direction);
        }

        private TilingNode? FindAdjacentX(int direction)
        {
            if ((Parent is SplitPanelNode sp && sp.Orientation == PanelOrientation.Horizontal) || Parent is StackPanelNode)
            {
                int index = Parent.IndexOf(this);
                int nextIndex = index + direction;
                if (Parent.Children.Count == nextIndex || nextIndex == -1)
                {
                    return Parent.FindAdjacentX(direction);
                }
                return Parent.Children[nextIndex];
            }
            return Parent?.FindAdjacentX(direction);
        }

        public TilingNode? GetAdjacentNode(TilingDirection direction)
        {
            return direction switch
            {
                TilingDirection.Down => FindAdjacentY(direction: 1),
                TilingDirection.Up => FindAdjacentY(direction: -1),
                TilingDirection.Left => FindAdjacentX(direction: -1),
                TilingDirection.Right => FindAdjacentX(direction: 1),
                _ => throw new ArgumentException(null, nameof(direction)),
            };
        }

        public WindowNode? GetAdjacentWindow(TilingDirection direction)
        {
            var adjacentNode = GetAdjacentNode(direction);
            if (direction == TilingDirection.Left || direction == TilingDirection.Up)
            {
                return adjacentNode?.Windows?.LastOrDefault();
            }
            else
            {
                return adjacentNode?.Windows?.FirstOrDefault();
            }
        }

        public void Embed(PanelNode panel)
        {
            if (Parent == null)
            {
                throw new InvalidOperationException();
            }
            var originalParent = Parent;
            var originalIndex = originalParent.Children.TakeWhile(x => x != this).Count();
            originalParent.ReplaceReference(originalIndex, panel);
            panel.Attach(this);
        }

        public virtual object Clone()
        {
            var copy = (TilingNode)MemberwiseClone();
            copy.Parent = null;
            return copy;
        }

        internal void Measure()
        {
            MeasureCore();
        }

        internal abstract void MeasureCore();

        internal void Arrange(RectangleF availableArea)
        {
            if (Parent != null)
            {
                Rectangle = availableArea.Pad(new RectangleF(Padding));
            }
            else
            {
                Rectangle = availableArea;
            }
            ArrangeCore(Rectangle);
        }

        internal abstract void ArrangeCore(RectangleF rectangle);
    }

    //class StackPanel : Panel
    //{
    //    public override NodeType Type => NodeType.Stack;
    //}
}
