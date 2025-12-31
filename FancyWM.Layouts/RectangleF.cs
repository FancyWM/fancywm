using System;

using WinMan;

namespace FancyWM.Layouts
{
    public readonly struct RectangleF : IEquatable<RectangleF>
    {
        public static RectangleF Empty => new();

        public double Left { get; }
        public double Top { get; }
        public double Right { get; }
        public double Bottom { get; }
        public readonly double Width => Math.Abs(Right - Left);
        public readonly double Height => Math.Abs(Bottom - Top);

        public double Area => Width * Height;

        public static RectangleF OffsetAndSize(double left, double top, double width, double height)
        {
            return new RectangleF(left, top, left + width, top + height);
        }

        public RectangleF(double left, double top, double right, double bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public RectangleF(Rectangle rectangle)
        {
            Left = rectangle.Left;
            Top = rectangle.Top;
            Right = rectangle.Right;
            Bottom = rectangle.Bottom;
        }

        public readonly Rectangle ToRectangle()
        {
            return new Rectangle((int)Left, (int)Top, (int)Right, (int)Bottom);
        }

        public readonly bool Equals(RectangleF other)
        {
            return Left == other.Left
                && Top == other.Top
                && Right == other.Right
                && Bottom == other.Bottom;
        }

        public override readonly bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public static bool operator ==(RectangleF lhs, RectangleF rhs)
        {
            return lhs.Equals(rhs);
        }
        public static bool operator !=(RectangleF lhs, RectangleF rhs)
        {
            return !lhs.Equals(rhs);
        }

        public readonly RectangleF Pad(RectangleF padding)
        {
            return new RectangleF(
                    left: Left + padding.Left,
                    top: Top + padding.Top,
                    right: Right - padding.Right,
                    bottom: Bottom - padding.Bottom);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
        }

        public override string ToString()
        {
            return $"(L={Left}, T={Top}, R={Right}, B={Bottom}, W={Width}, H={Height})";
        }
    }
}
