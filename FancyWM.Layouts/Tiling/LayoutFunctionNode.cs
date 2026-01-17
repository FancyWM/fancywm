using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public class LayoutFunctionNode(ILayoutFunction layoutFunction) : GridLikeNode
    {
        public override TilingNodeType Type => TilingNodeType.Static;

        public override IReadOnlyList<TilingNode> Children => m_children;

        public ILayoutFunction LayoutFunction { get; } = layoutFunction;


        private List<TilingNode> m_children = [];

        internal override void SetReference(int index, TilingNode node)
        {
            m_children[index] = node;
        }

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

        internal override void ArrangeCore(RectangleF rectangle)
        {
            var constraints = m_children.Select(_ => new Constraints(new Point(0, 0), new Point(short.MaxValue, short.MaxValue)));
            var rects = LayoutFunction.Execute(rectangle.ToRectangle(), constraints);
            for (int i = 0; i < m_children.Count; i++)
            {
                m_children[i].Arrange(new RectangleF(rects[i]));
            }
        }

        public override object Clone()
        {
            var copy = (LayoutFunctionNode)base.Clone();
            copy.m_children = m_children.Select(x => (TilingNode)x.Clone()).ToList();
            foreach (var child in copy.m_children)
            {
                child.Parent = copy;
            }
            return copy;
        }

        internal override void MeasureCore()
        {
            foreach (var node in m_children)
            {
                node.Measure();
            }
        }

        public override void Move(int fromIndex, int toIndex)
        {
            var n = m_children[fromIndex];
            m_children.RemoveAt(fromIndex);
            m_children.Insert(toIndex, n);
        }

        public override Point GetMaxChildSize(TilingNode node)
        {
            return ComputedContentRectangle.Size;
        }

        public override Point GetMaxSizeForInsert(TilingNode node)
        {
            return ComputedContentRectangle.Size;
        }

        public override bool CanResizeInOrientation(PanelOrientation orientation)
        {
            return false;
        }

        public override bool ResizeTo(TilingNode node, double newLength, GrowDirection direction)
        {
            return false;
        }

        public override bool ResizeBy(TilingNode node, double delta, GrowDirection direction)
        {
            return false;
        }
    }
}
