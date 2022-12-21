using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public class WindowNode : TilingNode
    {
        public override TilingNodeType Type => TilingNodeType.Window;

        public IWindow WindowReference { get; private set; }

        public override IEnumerable<WindowNode> Windows => new[] { this };

        public override IEnumerable<TilingNode> Nodes => new[] { this };

        public WindowNode(IWindow window)
        {
            WindowReference = window;
        }

        internal override void ArrangeCore(RectangleF rectangle)
        {
        }

        private static string Truncate(string value, int maxChars)
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + (char)0x2026;
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
