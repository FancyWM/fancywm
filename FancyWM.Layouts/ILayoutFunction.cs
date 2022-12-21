using System.Collections.Generic;

using WinMan;

namespace FancyWM.Layouts
{
    public struct Constraints
    {
        public Point MinSize { get; set; }
        public Point MaxSize { get; set; }
        public Point RequestedSize { get; set; }

        public Constraints(Point minSize, Point maxSize, Point requestedSize)
        {
            MinSize = minSize;
            MaxSize = maxSize;
            RequestedSize = requestedSize;
        }

        public Constraints(Point minSize, Point maxSize)
        {
            MinSize = minSize;
            MaxSize = maxSize;
            RequestedSize = maxSize;
        }
    }

    public interface ILayoutFunction
    {
        IReadOnlyList<Rectangle> Execute(Rectangle availableArea, IEnumerable<Constraints> constraints);
    }
}
