using System;
using System.Runtime.CompilerServices;

namespace FancyWM.Layouts
{
    internal static class Numbers
    {

        public const double Epsilon = 1E-7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Eq(this double x, double y)
        {
            return Math.Abs(x - y) < Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Neq(this double x, double y)
        {
            return !x.Eq(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Lt(this double x, double y)
        {
            return y - x > Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Gt(this double x, double y)
        {
            return x - y > Epsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Lte(this double x, double y)
        {
            return x.Lt(y) || x.Eq(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Gte(this double x, double y)
        {
            return x.Gt(y) || x.Eq(y);
        }
    }
}
