using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public class WindowNode(IWindow window) : TilingNode
    {
        public override TilingNodeType Type => TilingNodeType.Window;

        public IWindow WindowReference { get; private set; } = window;

        public override IEnumerable<WindowNode> Windows => [this];

        public override IEnumerable<TilingNode> Nodes => new[] { this };

        internal override void ArrangeCore(RectangleF rectangle)
        {
        }

        internal override void MeasureCore()
        {
            try
            {
                var minSize = WindowReference.MinSize;
                if (minSize.HasValue)
                {
                    MinSize = minSize.Value;
                }
                else
                {
                    MinSize = new Point(0, 0);
                }
            }
            catch
            {
                MinSize = new Point(0, 0);
            }
        }

    }

    //class StackPanel : Panel
    //{
    //    public override NodeType Type => NodeType.Stack;
    //}
}
