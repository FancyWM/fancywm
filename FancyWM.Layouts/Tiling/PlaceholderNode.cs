using System.Collections.Generic;

namespace FancyWM.Layouts.Tiling
{
    public class PlaceholderNode : TilingNode
    {
        public override TilingNodeType Type => TilingNodeType.Placeholder;

        public override IEnumerable<WindowNode> Windows => new WindowNode[0];

        public override IEnumerable<TilingNode> Nodes => new[] { this };

        public PlaceholderNode()
        {
        }

        internal override void ArrangeCore(RectangleF rectangle)
        {
        }

        internal override void MeasureCore()
        {
        }
    }
}
