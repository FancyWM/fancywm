using System;

using WinMan;

namespace FancyWM.Layouts
{
    public struct RectangleF : IEquatable<RectangleF>
    {
        public static RectangleF Empty => new RectangleF();

        public double Left { get; }
        public double Top { get; }
        public double Right { get; }
        public double Bottom { get; }
        public double Width => Right - Left;
        public double Height => Bottom - Top;

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

        public Rectangle ToRectangle()
        {
            return new Rectangle((int)Left, (int)Top, (int)Right, (int)Bottom);
        }

        public bool Equals(RectangleF other)
        {
            return Left == other.Left
                && Top == other.Top
                && Right == other.Right
                && Bottom == other.Bottom;
        }

        public override bool Equals(object? obj)
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

        public RectangleF Pad(RectangleF padding)
        {
            return new RectangleF(
                    left: Left + padding.Left,
                    top: Top + padding.Top,
                    right: Right - padding.Right,
                    bottom: Bottom - padding.Bottom);
        }

        public override int GetHashCode()
        {
            int hashCode = -1819631549;
            hashCode = hashCode * -1521134295 + Left.GetHashCode();
            hashCode = hashCode * -1521134295 + Top.GetHashCode();
            hashCode = hashCode * -1521134295 + Right.GetHashCode();
            hashCode = hashCode * -1521134295 + Bottom.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return $"(L={Left}, T={Top}, R={Right}, B={Bottom}, W={Width}, H={Height})";
        }
    }
}
