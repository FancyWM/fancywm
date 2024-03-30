using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FancyWM.Utilities
{
    internal class CountdownTimer
    {
        public Dispatcher Dispatcher => m_dispatcher;

        public bool IsDone => m_tcs?.Task.IsCompleted != false;

        private readonly Stopwatch m_stopwatch = new();
        private readonly Dispatcher m_dispatcher;

        private long m_endsAtTime;
        private TaskCompletionSource<object?>? m_tcs;

        public CountdownTimer() : this(Dispatcher.CurrentDispatcher) { }

        public CountdownTimer(Dispatcher dispatcher)
        {
            m_dispatcher = dispatcher;
            m_stopwatch.Start();
        }

        public Task SetRemainingAsync(TimeSpan remaining)
        {
            lock (m_stopwatch)
            {
                m_endsAtTime = m_stopwatch.ElapsedMilliseconds + (long)remaining.TotalMilliseconds;
                if (m_tcs == null)
                {
                    m_tcs = new TaskCompletionSource<object?>();
                    try
                    {
                        return m_tcs.Task;
                    }
                    finally
                    {
                        m_dispatcher.BeginInvoke(new Func<Task>(async () =>
                        {
                            long delay;
                            do
                            {
                                delay = m_endsAtTime - m_stopwatch.ElapsedMilliseconds;
                                if (delay > 0)
                                {
                                    await Task.Delay((int)delay);
                                }
                            } while (delay > 0);

                            m_tcs.SetResult(null);
                            m_tcs = null;
                        }));
                    }
                }
                return m_tcs!.Task;
            }
        }
    }
}
