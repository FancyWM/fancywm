using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public class StackPanelNode : PanelNode
    {
        public override TilingNodeType Type => TilingNodeType.Stack;

        public override IReadOnlyList<TilingNode> Children => m_children;

        private List<TilingNode> m_children = [];

        protected override void AttachCore(int index, TilingNode node)
        {
            m_children.Insert(index, node);
        }

        protected override void DetachCore(TilingNode node)
        {
            if (!m_children.Remove(node))
            {
                throw new InvalidOperationException();
            }
        }

        internal override void ArrangeCore(RectangleF rect)
        {
            foreach (var child in m_children)
            {
                RectangleF childRect = rect;
                if (child is WindowNode)
                {
                    RectangleF padding = new(Spacing / 2, Spacing / 2, Spacing / 2, Spacing / 2);
                    childRect = childRect.Pad(padding);
                }
                child.Arrange(childRect);
            }
        }

        internal override void SetReference(int index, TilingNode node)
        {
            m_children[index] = node;
        }

        public override object Clone()
        {
            var copy = (StackPanelNode)base.Clone();
            copy.m_children = m_children.Select(x => (TilingNode)x.Clone()).ToList();
            foreach (var child in copy.m_children)
            {
                child.Parent = copy;
            }
            return copy;
        }

        public override Point ComputeFreeSize()
        {
            return new(ComputedRectangle.Width, ComputedRectangle.Height);
        }

        internal override void MeasureCore()
        {
            int width = 0, height = 0;
            foreach (var child in Children)
            {
                child.Measure();
                var minChild = child.MinSize;
                width = Math.Max(width, minChild.X);
                height = Math.Max(height, minChild.Y);
            }
            MinSize = new Point(width, height);
        }

        public override void Move(int fromIndex, int toIndex)
        {
            var n = m_children[fromIndex];
            m_children.RemoveAt(fromIndex);
            m_children.Insert(toIndex, n);
        }
    }

    //class StackPanel : Panel
    //{
    //    public override NodeType Type => NodeType.Stack;
    //}
}
