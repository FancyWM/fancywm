using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public class StaticPanelNode(ILayoutFunction layoutFunction) : PanelNode
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
            var copy = (StaticPanelNode)base.Clone();
            copy.m_children = m_children.Select(x => (TilingNode)x.Clone()).ToList();
            foreach (var child in copy.m_children)
            {
                child.Parent = copy;
            }
            return copy;
        }

        public override Point ComputeFreeSize()
        {
            throw new NotImplementedException();
        }

        internal override void MeasureCore()
        {
            throw new NotImplementedException();
        }

        public override void Move(int fromIndex, int toIndex)
        {
            throw new NotImplementedException();
        }
    }
}
