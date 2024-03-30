using System;

namespace FancyWM.Utilities
{
    internal interface IEasingFunction
    {
        double Evaluate(double x);
    }

    internal static class EasingFunction
    {
        private class LambdaEasingFunction : IEasingFunction
        {
            Func<double, double> m_func;

            public LambdaEasingFunction(Func<double, double> func)
            {
                m_func = func;
            }

            public double Evaluate(double x)
            {
                x = Math.Min(1.0, Math.Max(0.0, x));
                return m_func(x);
            }
        }

        public static IEasingFunction Create(Func<double, double> func)
        {
            return new LambdaEasingFunction(func);
        }

        public static IEasingFunction EaseInOutCirc => Create(x => x < 0.5
            ? (1 - Math.Sqrt(1 - Math.Pow(2 * x, 2))) / 2
            : (Math.Sqrt(1 - Math.Pow(-2 * x + 2, 2)) + 1) / 2);

        public static IEasingFunction EaseOutBouce => Create(x =>
        {
            var n1 = 7.5625;
            var d1 = 2.75;

            if (x < 1 / d1)
            {
                return n1 * x * x;
            }
            else if (x < 2 / d1)
            {
                return n1 * (x -= 1.5 / d1) * x + 0.75;
            }
            else if (x < 2.5 / d1)
            {
                return n1 * (x -= 2.25 / d1) * x + 0.9375;
            }
            else
            {
                return n1 * (x -= 2.625 / d1) * x + 0.984375;
            }
        });
    }

    internal class CubicBezierCurve : IEasingFunction
    {
        public double P0 { get; }
        public double P1 { get; }
        public double P2 { get; }
        public double P3 { get; }

        public CubicBezierCurve(double p0, double p1, double p2, double p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public double Evaluate(double x)
        {
            x = Math.Min(1.0, Math.Max(0.0, x));
            double xi = 1 - x;
            return P0 * xi * xi * xi + 3 * P1 * xi * xi * x + 3 * P2 * xi * x * x + P3 * x * x * x;
        }
    }
}
