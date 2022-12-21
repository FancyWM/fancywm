using System;
using System.Threading;

namespace FancyWM.Utilities
{
    internal class Counter
    {
        private int m_count = 0;

        public int Count
        {
            get
            {
                int c = m_count;
                return c > 0 ? c : 0;
            }
        }

        public void Increment()
        {
            Interlocked.Increment(ref m_count);
        }

        public void Decrement()
        {
            while (true)
            {
                int exp = m_count;
                if (exp <= 0)
                {
                    throw new InvalidOperationException("Count went below zero!");
                }

                if (Interlocked.CompareExchange(ref m_count, exp - 1, exp) == exp)
                {
                    break;
                }
            }
        }

        public bool DecrementIfPositive()
        {
            while (true)
            {
                int exp = m_count;
                if (exp <= 0)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref m_count, exp - 1, exp) == exp)
                {
                    return true;
                }
            }
        }

        public bool IsZero()
        {
            return m_count <= 0;
        }

        public bool IsPositive()
        {
            return m_count > 0;
        }
    }
}
