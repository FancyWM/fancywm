using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using WinMan;

using static FancyWM.Layouts.Flex;

namespace FancyWM.Layouts.Tiling
{
    public class SplitPanelNode : GridLikeNode
    {
        public override TilingNodeType Type => TilingNodeType.Split;

        public override IReadOnlyList<TilingNode> Children => m_children;

        public PanelOrientation Orientation { get; set; }

        private List<TilingNode> m_children = [];

        private Flex m_constraints;

        public SplitPanelNode()
        {
            m_constraints = new Flex();
            m_constraints.SetContainerWidth(1);
        }

        protected override void AttachCore(int index, TilingNode node)
        {
            m_children.Insert(index, node);
            m_constraints.InsertItem(index, 0, 0);
        }

        protected override void DetachCore(TilingNode node)
        {
            int index = m_children.IndexOf(node);
            if (index == -1)
            {
                throw new InvalidOperationException();
            }

            m_children.RemoveAt(index);
            m_constraints.RemoveItem(index);
        }

        internal override void MeasureCore()
        {
            int width = 0;
            int height = 0;
            if (Orientation == PanelOrientation.Horizontal)
            {
                foreach (var child in Children)
                {
                    child.Measure();
                    var childRect = child.MinSize;
                    width += childRect.X;
                    height = Math.Max(height, childRect.Y);
                }
            }
            else
            {
                foreach (var child in Children)
                {
                    child.Measure();
                    var childRect = child.MinSize;
                    height += childRect.Y;
                    width = Math.Max(width, childRect.X);
                }
            }
            var spacing = (m_children.OfType<WindowNode>().Count() + 1) * Spacing / 2;
            ContentMinSize = new Point(width + spacing + Padding.Left + Padding.Right, height + spacing + Padding.Top + Padding.Bottom);
            ContentMaxSize = new Point(short.MaxValue, short.MaxValue);
        }

        internal override void ArrangeCore(RectangleF rect)
        {
            if (Parent == null)
            {
                rect = rect.Pad(new RectangleF(Spacing / 2, Spacing / 2, Spacing / 2, Spacing / 2));
            }

            var containerWidth = Orientation == PanelOrientation.Horizontal
                ? rect.Width
                : rect.Height;
            m_constraints.SetContainerWidth(containerWidth);

            var newConstraints = m_constraints
                .Select(x => (x.MinWidth, x.MaxWidth))
                .ToArray();
            for (int i = 0; i < m_children.Count; i++)
            {
                var minWidth = Orientation == PanelOrientation.Horizontal
                    ? m_children[i].MinSize.X
                    : m_children[i].MinSize.Y;
                if (m_children[i] is WindowNode)
                {
                    minWidth += Spacing;
                }

                var maxWidth = Orientation == PanelOrientation.Horizontal
                    ? m_children[i].MaxSize.X
                    : m_children[i].MaxSize.Y;

                if (m_constraints[i].MaxWidth == 0)
                {
                    // Initialize
                    m_constraints.RemoveItem(i);
                    try
                    {
                        m_constraints.InsertItem(i, minWidth, maxWidth);
                    }
                    catch (UnsatisfiableFlexConstraintsException)
                    {
                        // Keep a consistent state
                        m_constraints.InsertItem(i, 0, 0);
                        throw;
                    }
                }
                newConstraints[i].MinWidth = minWidth;
                newConstraints[i].MaxWidth = maxWidth;
            }

            m_constraints.UpdateConstraints(newConstraints);

            RectangleF lastRect = RectangleF.OffsetAndSize(rect.Left, rect.Top, 0, 0);
            for (int i = 0; i < m_children.Count; i++)
            {
                var child = m_children[i];
                var constraints = m_constraints[i];
                RectangleF childRect;
                if (Orientation == PanelOrientation.Horizontal)
                {
                    var w = constraints.Width;
                    childRect = RectangleF.OffsetAndSize(lastRect.Right, rect.Top, w, rect.Height);
                    lastRect = childRect;
                }
                else
                {
                    var h = constraints.Width;
                    childRect = RectangleF.OffsetAndSize(rect.Left, lastRect.Bottom, rect.Width, h);
                    lastRect = childRect;
                }

                if (child is WindowNode)
                {
                    RectangleF padding = new(Spacing / 2, Spacing / 2, Spacing / 2, Spacing / 2);
                    childRect = childRect.Pad(padding);
                }
                child.Arrange(childRect);
            }
        }

        public override bool CanResizeInOrientation(PanelOrientation orientation)
        {
            return Orientation == orientation;
        }

        public override bool ResizeTo(TilingNode node, double newLength, GrowDirection direction)
        {
            if (Children.Count == 1)
            {
                return false;
            }

            int index = m_children.IndexOf(node);
            return ResizeBy(node, newLength - m_constraints[index].Width, direction);
        }

        public override bool ResizeBy(TilingNode node, double delta, GrowDirection direction)
        {
            if (Children.Count == 1)
            {
                return false;
            }

            try
            {
                int index = m_children.IndexOf(node);
                var item = m_children[index];
                var length = m_constraints.ContainerWidth;
                var newWeight = (m_constraints[index].Width + delta) / length;

                double minWeight = 1.0 / 16;
                double clampedWeight = Math.Min(1.0 - minWeight, Math.Max(minWeight, newWeight));
                m_constraints.ResizeItem(index, clampedWeight * length, direction);
                return true;
            }
            catch (UnsatisfiableFlexConstraintsException e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        internal override void SetReference(int index, TilingNode node)
        {
            m_children[index] = node;
        }

        public override object Clone()
        {
            var copy = (SplitPanelNode)base.Clone();
            copy.m_children = m_children.Select(x => (TilingNode)x.Clone()).ToList();
            foreach (var child in copy.m_children)
            {
                child.Parent = copy;
            }
            copy.m_constraints = new Flex(m_constraints);
            return copy;
        }

        public override Point GetMaxChildSize(TilingNode node)
        {
            if (!Children.Contains(node))
            {
                throw new InvalidOperationException($"Node {node} is not a child of {this}");
            }

            var maxReclaim = (int)(m_constraints.ContainerWidth - m_constraints.MinWidth);
            if (Orientation == PanelOrientation.Horizontal)
            {
                return new Point(node.ComputedRectangle.Width + maxReclaim, node.ComputedRectangle.Height);
            }
            return new Point(node.ComputedRectangle.Width, node.ComputedRectangle.Height + maxReclaim);
        }

        public override Point GetMaxSizeForInsert(TilingNode node)
        {
            var spacing = node is WindowNode ? Spacing : 0;
            var contentRectangle = new Rectangle(
                ComputedRectangle.Left + Padding.Left + spacing / 2,
                ComputedRectangle.Top + Padding.Top + spacing / 2,
                ComputedRectangle.Right - Padding.Right - spacing / 2,
                ComputedRectangle.Bottom - Padding.Bottom - spacing / 2);

            if (Orientation == PanelOrientation.Horizontal)
            {
                return new Point(contentRectangle.Width - (int)m_constraints.MinWidth, contentRectangle.Height);
            }
            else
            {
                return new Point(contentRectangle.Width, contentRectangle.Height - (int)m_constraints.MinWidth);
            }
        }

        public override void Move(int fromIndex, int toIndex)
        {
            var n = m_children[fromIndex];
            m_children.RemoveAt(fromIndex);
            m_children.Insert(toIndex, n);

            m_constraints.MoveItem(fromIndex, toIndex);
        }
    }

    //class StackPanel : Panel
    //{
    //    public override NodeType Type => NodeType.Stack;
    //}
}
