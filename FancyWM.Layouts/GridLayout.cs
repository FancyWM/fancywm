using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts
{
    public class GridLayout : ILayoutFunction
    {
        public int Spacing { get; }

        public GridLayout(int spacing)
        {
            Spacing = spacing;
        }

        public IReadOnlyList<Rectangle> Execute(Rectangle availableArea, IEnumerable<Constraints> constraints)
        {
            bool isHorizontal = availableArea.Width >= availableArea.Height;

            var count = constraints.Count();
            var root = (int)Math.Round(Math.Sqrt(count));

            var w = (availableArea.Width - Spacing * (root + 1)) / root;
            var h = (availableArea.Height - Spacing * (root + 1)) / root;

            List<Rectangle> rects = new List<Rectangle>();
            for (int i = 0; i < w; i++)
            {
                for (int j = 0; j < h; j++)
                {

                }
            }

            return constraints.Select((rect, index) => Rectangle.OffsetAndSize(
                left: availableArea.Left + (w * index) + Spacing * (index + 1),
                top: availableArea.Top + Spacing,
                width: w,
                height: h - 2 * Spacing
            )).ToArray();
        }
    }
}
