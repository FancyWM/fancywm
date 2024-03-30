using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts
{
    public class RatioLayout : ILayoutFunction
    {
        public double Ratio { get; }
        public int Spacing { get; }

        public RatioLayout(double ratio, int spacing)
        {
            if (0 >= ratio || ratio >= 1)
            {
                throw new ArgumentException("0 < Ratio < 1", nameof(ratio));
            }
            Ratio = ratio;
            Spacing = spacing;
        }

        public IReadOnlyList<Rectangle> Execute(Rectangle availableArea, IEnumerable<Constraints> constraints)
        {
            if (!constraints.Any())
            {
                return [];
            }

            if (constraints.Count() == 1)
            {
                return
                [
                    new Rectangle(
                        left: availableArea.Left + Spacing,
                        top: availableArea.Top + Spacing,
                        bottom: availableArea.Bottom - Spacing,
                        right: availableArea.Right - Spacing
                    )
                ];
            }

            Rectangle currentArea, nextArea;
            if (availableArea.Width >= availableArea.Height)
            {
                // Vertically split space
                currentArea = Rectangle.OffsetAndSize(
                    left: availableArea.Left + Spacing,
                    top: availableArea.Top + Spacing,
                    width: (int)(availableArea.Width * Ratio) - Spacing / 2,
                    height: availableArea.Height - Spacing * 2
                );
                nextArea = Rectangle.OffsetAndSize(
                    left: availableArea.Left + (int)(availableArea.Width * Ratio) + Spacing / 2,
                    top: availableArea.Top,
                    width: (int)(availableArea.Width * (1 - Ratio)) - Spacing / 2,
                    height: availableArea.Height
                );
            }
            else
            {
                // Horizontally split space
                currentArea = Rectangle.OffsetAndSize(
                    left: availableArea.Left + Spacing,
                    top: availableArea.Top + Spacing,
                    width: availableArea.Width - Spacing * 2,
                    height: (int)(availableArea.Height * Ratio) - Spacing / 2
                );
                nextArea = Rectangle.OffsetAndSize(
                    left: availableArea.Left,
                    top: (int)(availableArea.Top + availableArea.Height * Ratio) + Spacing / 2,
                    width: availableArea.Width,
                    height: (int)(availableArea.Height * (1 - Ratio)) - Spacing / 2
                );
            }

            var rest = Execute(nextArea, constraints.Skip(1));
            return [.. (new Rectangle[] { currentArea }), .. rest];
        }
    }
}
