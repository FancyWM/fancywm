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
                    ContentMinSize = new Point(minSize.Value.X + Padding.Left + Padding.Right, minSize.Value.Y + Padding.Top + Padding.Bottom);
                }
                else
                {
                    ContentMinSize = new Point(Padding.Left + Padding.Right, Padding.Top + Padding.Bottom);
                }
            }
            catch
            {
                ContentMinSize = new Point(Padding.Left + Padding.Right, Padding.Top + Padding.Bottom);
            }
        }

    }

    //class StackPanel : Panel
    //{
    //    public override NodeType Type => NodeType.Stack;
    //}
}
