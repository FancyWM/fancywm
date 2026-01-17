using System;
using System.Collections.Generic;
using System.Linq;
using WinMan;

namespace FancyWM.Layouts
{
    public class GridLayout(int spacing) : ILayoutFunction
    {
        public int Spacing { get; } = spacing;

        public IReadOnlyList<Rectangle> Execute(Rectangle availableArea, IEnumerable<Constraints> constraints)
        {
            var (rows, columns) = CalculateGridDimensions(constraints.Count(), availableArea.Width, availableArea.Height);

            var constraintList = constraints.ToList();
            var result = new List<Rectangle>();

            if (constraintList.Count == 0)
            {
                return result;
            }

            double cellWidth = (double)availableArea.Width / columns;
            double cellHeight = (double)availableArea.Height / rows;

            for (int i = 0; i < constraintList.Count; i++)
            {
                if (i >= rows * columns)
                {
                    break;
                }

                int row = i / columns;
                int col = i % columns;

                int slotLeft = availableArea.Left + (int)(col * cellWidth);
                int slotRight = (col == columns - 1) 
                    ? availableArea.Right 
                    : availableArea.Left + (int)((col + 1) * cellWidth);
                
                int slotTop = availableArea.Top + (int)(row * cellHeight);
                int slotBottom = (row == rows - 1) 
                    ? availableArea.Bottom 
                    : availableArea.Top + (int)((row + 1) * cellHeight);

                int left = slotLeft + (col == 0 ? Spacing : Spacing / 2);
                int right = slotRight - (col == columns - 1 ? Spacing : Spacing / 2);
                int top = slotTop + (row == 0 ? Spacing : Spacing / 2);
                int bottom = slotBottom - (row == rows - 1 ? Spacing : Spacing / 2);

                result.Add(new Rectangle(
                    left: Math.Min(left, right),
                    top: Math.Min(top, bottom),
                    bottom: Math.Max(top, bottom),
                    right: Math.Max(left, right)
                ));
            }

            return result;
        }

        private (int rows, int columns) CalculateGridDimensions(int count, int width, int height)
        {
            int a = 1;
            while (a * a < count) a++;
            int b = (count + a - 1) / a;

            if (width > height && a >= b)
            {
                (b, a) = (a, b);
            }

            return (a, b);
        }
    }
}
